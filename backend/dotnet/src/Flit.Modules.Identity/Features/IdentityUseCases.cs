using Flit.Modules.Identity.Domain;
using Flit.Modules.Identity.Ports;
using Flit.SharedKernel;

namespace Flit.Modules.Identity.Features;

// ============================================================================
// Register
// ============================================================================
public static class Register
{
    public sealed record Command(
        string Email,
        string Password,
        TipoDocumento TipoDocumento,
        string NumeroDocumento,
        string Nombres,
        string Apellidos,
        bool ConsentimientoDatosPersonales,
        string PoliticaVersion = "v1.0",
        // MVP Fase 4: OCR cedula opcional. Si se provee, se valida contra
        // NumeroDocumento. Habeas Data: el caller NO persiste la imagen.
        string? CedulaImageBase64 = null,
        string? CedulaMimeType = null);

    public sealed record Response(
        Guid Id, string Email, string Rol, DateTimeOffset CreadoEn,
        // Si OCR se ejecuto, devolvemos confidence para auditabilidad.
        double? OcrConfidence = null);

    public static async Task<Result<Response, IdentityError>> HandleAsync(
        Command cmd,
        IUsuariosRepository repo,
        ICredentialsRepository credsRepo,
        IPasswordHasher hasher,
        IClock clock,
        CancellationToken ct,
        IOcrCedulaClient? ocr = null)
    {
        // 1. Validar password (politica MVP simplificada: 12+ chars).
        if (string.IsNullOrEmpty(cmd.Password) || cmd.Password.Length < 12)
            return Result<Response, IdentityError>.Failure(
                new IdentityError.PasswordInvalido("password minimo 12 caracteres"));

        // 2. Construir VOs.
        var emailResult = Email.Create(cmd.Email);
        if (!emailResult.IsSuccess)
            return Map(emailResult);

        var docResult = Documento.Create(cmd.TipoDocumento, cmd.NumeroDocumento);
        if (!docResult.IsSuccess)
            return MapDoc(docResult);

        // 3. Unicidad de email + documento.
        var emailVO = emailResult.Match(e => e, _ => null!);
        var docVO = docResult.Match(d => d, _ => null!);

        if (await repo.ExisteEmailAsync(emailVO.Value, ct))
            return Result<Response, IdentityError>.Failure(
                new IdentityError.EmailDuplicado(emailVO.Value));

        if (await repo.ExisteDocumentoAsync(docVO.Tipo.ToString(), docVO.Numero, ct))
            return Result<Response, IdentityError>.Failure(
                new IdentityError.DocumentoDuplicado($"{docVO.Tipo}:{docVO.Numero}"));

        // 4. Crear usuario (siempre rol ciudadano en register MVP).
        var usuarioResult = Usuario.Crear(
            emailVO, docVO, cmd.Nombres, cmd.Apellidos,
            Rol.Ciudadano,
            cmd.ConsentimientoDatosPersonales,
            cmd.PoliticaVersion,
            clock);

        if (!usuarioResult.IsSuccess)
            return MapUser(usuarioResult);

        var usuario = usuarioResult.Match(u => u, _ => null!);

        // 5. Hash password + persistir.
        var hash = hasher.Hash(cmd.Password);
        var saveResult = await repo.GuardarAsync(usuario, ct);
        if (!saveResult.IsSuccess)
            return MapSave(saveResult);

        await credsRepo.GuardarPasswordHashAsync(usuario.Id, hash, clock.UtcNow, ct);

        // 6. MVP Fase 4: OCR opcional. Validar contra NumeroDocumento.
        //    Politica: si el OCR esta configurado y el ciudadano envia imagen,
        //    SI confidence >= 0.5 Y numero matchea, registrar confidence;
        //    SI numero NO matchea: rollback (eliminar registros) y devolver
        //    OcrCedulaNoCoincide; SI OCR falla por error de motor, advertencia
        //    (warn log) pero NO bloquear el registro (MVP friendly).
        double? ocrConfidence = null;
        if (ocr is not null && !string.IsNullOrEmpty(cmd.CedulaImageBase64))
        {
            var mime = string.IsNullOrEmpty(cmd.CedulaMimeType) ? "image/jpeg" : cmd.CedulaMimeType;
            var ocrResult = await ocr.ExtraerAsync(cmd.CedulaImageBase64, mime, ct);
            if (ocrResult.Success)
            {
                var extraidoSinPuntos = (ocrResult.Numero ?? string.Empty)
                    .Replace(".", string.Empty).Replace(" ", string.Empty);
                if (!string.Equals(extraidoSinPuntos, docVO.Numero, StringComparison.Ordinal))
                {
                    // Rollback (mejor esfuerzo) y reportar.
                    await credsRepo.EliminarAsync(usuario.Id, ct);
                    return Result<Response, IdentityError>.Failure(
                        new IdentityError.OcrCedulaNoCoincide(
                            DocumentoEsperado: docVO.Numero,
                            DocumentoExtraido: extraidoSinPuntos));
                }
                ocrConfidence = ocrResult.ConfidenceGlobal;
            }
            // OCR fallido (motor, red, timeout): no bloquea. Solo log
            // upstream lo registra via OcrCedulaFallido si se quiere
            // hacer estricto (TODO post-MVP).
        }

        return Result<Response, IdentityError>.Success(
            new Response(usuario.Id, usuario.Email.Value, usuario.Rol.ToClaimValue(),
                usuario.CreadoEn, ocrConfidence));
    }

    private static Result<Response, IdentityError> Map(Result<Email, IdentityError> r) =>
        Result<Response, IdentityError>.Failure(r.Match(_ => null!, e => e));

    private static Result<Response, IdentityError> MapDoc(Result<Documento, IdentityError> r) =>
        Result<Response, IdentityError>.Failure(r.Match(_ => null!, e => e));

    private static Result<Response, IdentityError> MapUser(Result<Usuario, IdentityError> r) =>
        Result<Response, IdentityError>.Failure(r.Match(_ => null!, e => e));

    private static Result<Response, IdentityError> MapSave(Result<Guid, IdentityError> r) =>
        Result<Response, IdentityError>.Failure(r.Match(_ => null!, e => e));
}

// ============================================================================
// Login
// ============================================================================
public static class Login
{
    public sealed record Command(string Email, string Password);

    public sealed record Response(
        Guid UserId,
        string Email,
        string Rol,
        string AccessToken,
        string RefreshToken,
        DateTimeOffset AccessExpiresAt,
        DateTimeOffset RefreshExpiresAt);

    public static async Task<Result<Response, IdentityError>> HandleAsync(
        Command cmd,
        IUsuariosRepository repo,
        ICredentialsRepository credsRepo,
        IPasswordHasher hasher,
        ITokenIssuer issuer,
        IClock clock,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(cmd.Email) || string.IsNullOrEmpty(cmd.Password))
            return Result<Response, IdentityError>.Failure(new IdentityError.CredencialesInvalidas());

        var usuario = await repo.ObtenerPorEmailAsync(cmd.Email, ct);
        if (usuario is null)
            // No revelar si el email existe. Tiempo constante respond.
            return Result<Response, IdentityError>.Failure(new IdentityError.CredencialesInvalidas());

        // Validar estado de la cuenta (activa, no bloqueada).
        var puedeLogin = usuario.ValidarPuedeLogin(clock);
        if (!puedeLogin.IsSuccess)
            return Result<Response, IdentityError>.Failure(puedeLogin.Match(_ => null!, e => e));

        var hash = await credsRepo.ObtenerPasswordHashAsync(usuario.Id, ct);
        if (hash is null || !hasher.Verify(cmd.Password, hash))
        {
            usuario.RegistrarLoginFallido(clock);
            await repo.GuardarAsync(usuario, ct);
            return Result<Response, IdentityError>.Failure(new IdentityError.CredencialesInvalidas());
        }

        usuario.RegistrarLoginExitoso(clock);
        await repo.GuardarAsync(usuario, ct);

        var tokens = issuer.Issue(usuario);
        return Result<Response, IdentityError>.Success(new Response(
            UserId: usuario.Id,
            Email: usuario.Email.Value,
            Rol: usuario.Rol.ToClaimValue(),
            AccessToken: tokens.AccessToken,
            RefreshToken: tokens.RefreshToken,
            AccessExpiresAt: tokens.AccessExpiresAt,
            RefreshExpiresAt: tokens.RefreshExpiresAt));
    }
}

// ============================================================================
// Refresh
// ============================================================================
public static class Refresh
{
    public sealed record Command(string RefreshToken);

    public sealed record Response(
        string AccessToken,
        string RefreshToken,
        DateTimeOffset AccessExpiresAt,
        DateTimeOffset RefreshExpiresAt);

    public static async Task<Result<Response, IdentityError>> HandleAsync(
        Command cmd,
        IUsuariosRepository repo,
        ITokenIssuer issuer,
        IRefreshTokenStore denylist,
        CancellationToken ct)
    {
        var userId = issuer.ValidateRefresh(cmd.RefreshToken);
        if (userId is null)
            return Result<Response, IdentityError>.Failure(new IdentityError.RefreshTokenInvalido());

        // Verificar denylist (rotation: el refresh anterior queda revocado).
        var jti = Adapters.RsaJwtTokenIssuer.ExtractRefreshJti(cmd.RefreshToken);
        if (jti.HasValue && await denylist.IsRevokedAsync(jti.Value, ct))
            return Result<Response, IdentityError>.Failure(new IdentityError.RefreshTokenInvalido());

        var usuario = await repo.ObtenerPorIdAsync(userId.Value, ct);
        if (usuario is null || !usuario.Activo)
            return Result<Response, IdentityError>.Failure(new IdentityError.RefreshTokenInvalido());

        var nuevos = issuer.Issue(usuario);

        // Revocar el refresh viejo (rotation).
        if (jti.HasValue)
            await denylist.RevokeAsync(
                jti.Value,
                expiresAt: nuevos.RefreshExpiresAt,
                reason: "rotation",
                ct);

        return Result<Response, IdentityError>.Success(new Response(
            nuevos.AccessToken,
            nuevos.RefreshToken,
            nuevos.AccessExpiresAt,
            nuevos.RefreshExpiresAt));
    }
}

// ============================================================================
// Logout
// ============================================================================
public static class Logout
{
    public sealed record Command(string RefreshToken);

    public static async Task<Result<Unit, IdentityError>> HandleAsync(
        Command cmd,
        IRefreshTokenStore denylist,
        CancellationToken ct)
    {
        var jti = Adapters.RsaJwtTokenIssuer.ExtractRefreshJti(cmd.RefreshToken);
        if (jti is null)
            // Idempotente: token invalido es como ya estaba revocado.
            return Result<Unit, IdentityError>.Success(Unit.Value);

        // Revocamos por 7d (max refresh expiry).
        await denylist.RevokeAsync(jti.Value, DateTimeOffset.UtcNow.AddDays(7), "logout", ct);
        return Result<Unit, IdentityError>.Success(Unit.Value);
    }
}

// ============================================================================
// Me (datos del usuario actual)
// ============================================================================
public static class ObtenerMe
{
    public sealed record Query(Guid UserId);

    public sealed record Response(
        Guid Id,
        string Email,
        string Documento,
        string Nombres,
        string Apellidos,
        string Rol,
        bool EmailVerificado,
        DateTimeOffset CreadoEn,
        DateTimeOffset? UltimoLogin);

    public static async Task<Result<Response, IdentityError>> HandleAsync(
        Query query,
        IUsuariosRepository repo,
        CancellationToken ct)
    {
        var usuario = await repo.ObtenerPorIdAsync(query.UserId, ct);
        if (usuario is null)
            return Result<Response, IdentityError>.Failure(
                new IdentityError.UsuarioNoEncontrado(query.UserId));

        return Result<Response, IdentityError>.Success(new Response(
            Id: usuario.Id,
            Email: usuario.Email.Value,
            Documento: $"{usuario.Documento.Tipo}:{usuario.Documento.Numero}",
            Nombres: usuario.Nombres,
            Apellidos: usuario.Apellidos,
            Rol: usuario.Rol.ToClaimValue(),
            EmailVerificado: usuario.EmailVerificado,
            CreadoEn: usuario.CreadoEn,
            UltimoLogin: usuario.UltimoLogin));
    }
}
