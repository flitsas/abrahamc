using System.Security.Cryptography;
using System.Text;
using Flit.Modules.Auth.Domain;
using Flit.Modules.Auth.Ports;
using Flit.Modules.Users.Application; // Unit
using Flit.Modules.Users.Domain;
using Flit.Modules.Users.Ports;
using Flit.SharedKernel;

namespace Flit.Modules.Auth.Application;

// =============================================================================
// Tipos compartidos
// =============================================================================

/// <summary>Tokens devueltos al cliente cuando login pasa (sin MFA) o tras validar MFA.</summary>
public sealed record AuthTokens(string AccessToken, string IdToken, string RefreshToken, int ExpiresInSeconds);

/// <summary>
/// Snapshot minimo del User autenticado para que el frontend tenga el UUID
/// interno (identity.users.id) sin tener que decodificar el JWT. Necesario
/// porque el sub de Cognito es string distinto al UUID de BD (ADR-0010).
/// </summary>
public sealed record AuthUserSnapshot(Guid Id, string Email, string FullName);

// =============================================================================
// Login (POST /api/v1/auth/login)
// =============================================================================
public static class Login
{
    public sealed record Command(string Email, string Password, string? IpAddress, string? UserAgent);

    public abstract record Response
    {
        /// <summary>Login sin MFA: tokens + user devueltos directamente.</summary>
        public sealed record TokensIssued(AuthTokens Tokens, AuthUserSnapshot User) : Response;
        /// <summary>Login con MFA: session_id para el segundo paso.</summary>
        public sealed record MfaRequired(Guid SessionId) : Response;
    }

    public abstract record LoginError(string Code, string Message)
    {
        public sealed record InvalidCredentials() : LoginError("INVALID_CREDENTIALS", "Credenciales invalidas");
        public sealed record CognitoOrphan() : LoginError("COGNITO_ORPHAN", "Usuario existe en Cognito pero no en BD");
        public sealed record UserNotActive(string Status) : LoginError("USER_NOT_ACTIVE", $"Usuario no activo (status={Status})");
        public sealed record CognitoUnavailable(string Detail) : LoginError("COGNITO_UNAVAILABLE", $"Cognito no disponible: {Detail}");
    }

    public static async Task<Result<Response, LoginError>> HandleAsync(
        Command cmd,
        IUsersRepository users,
        ICognitoDirectory cognito,
        IMfaSessionStore mfaStore,
        IClock clock,
        TimeSpan mfaSessionTtl,
        CancellationToken ct = default)
    {
        // 1. Cognito AdminInitiateAuth
        CognitoTokens tokens;
        try
        {
            tokens = await cognito.AdminInitiateAuthAsync(cmd.Email, cmd.Password, ct);
        }
        catch (Exception ex) when (ex.Message.Contains("NotAuthorized", StringComparison.OrdinalIgnoreCase))
        {
            return Result<Response, LoginError>.Failure(new LoginError.InvalidCredentials());
        }
        catch (Exception ex)
        {
            return Result<Response, LoginError>.Failure(new LoginError.CognitoUnavailable(ex.Message));
        }

        // 2. Decodificar IdToken para extraer cognito_sub.
        // En FLIT 2.0 el sub se obtiene del idToken sin validar firma (Cognito
        // acaba de emitirlo en este request — no hay MITM posible). Para el
        // resto de requests autenticados sí se valida con JWKS en el middleware.
        var sub = ExtractSubFromJwt(tokens.IdToken);
        if (sub is null)
            return Result<Response, LoginError>.Failure(
                new LoginError.CognitoUnavailable("IdToken sin claim 'sub'"));

        // 3. Buscar el user en BD local
        var user = await users.GetByCognitoSubAsync(sub, ct);
        if (user is null)
        {
            // Cognito orphan: usuario en Cognito sin contraparte en BD.
            // Cerrar todas sus sesiones por seguridad y registrar inconsistencia.
            await cognito.AdminUserGlobalSignOutAsync(cmd.Email, ct);
            return Result<Response, LoginError>.Failure(new LoginError.CognitoOrphan());
        }

        // 4. Validar status del user en BD
        if (user.Status != UserStatus.ACTIVE)
        {
            await cognito.AdminUserGlobalSignOutAsync(cmd.Email, ct);
            return Result<Response, LoginError>.Failure(
                new LoginError.UserNotActive(user.Status.ToString()));
        }

        // 5. Si MFA habilitado, retener tokens y devolver session_id
        if (user.MfaEnabled)
        {
            var pending = new PendingMfaSession(
                UserId: user.Id,
                CognitoSub: sub,
                Email: user.Email,
                Tokens: tokens,
                CreatedAt: clock.UtcNow,
                Attempts: 0);
            var sessionId = await mfaStore.CreateAsync(pending, mfaSessionTtl, ct);
            return Result<Response, LoginError>.Success(new Response.MfaRequired(sessionId));
        }

        // 6. Sin MFA: registrar login y devolver tokens + user snapshot
        user.RegisterLogin(clock.UtcNow);
        await users.UpdateAsync(user, ct);
        return Result<Response, LoginError>.Success(new Response.TokensIssued(
            new AuthTokens(tokens.AccessToken, tokens.IdToken, tokens.RefreshToken ?? string.Empty, tokens.ExpiresIn),
            new AuthUserSnapshot(user.Id, user.Email, user.FullName)));
    }

    /// <summary>
    /// Extrae el claim 'sub' del payload de un JWT sin validar firma.
    /// Solo seguro cuando el token viene directamente de la respuesta de
    /// Cognito en el mismo request (no hay MITM posible).
    /// </summary>
    private static string? ExtractSubFromJwt(string jwt)
    {
        try
        {
            var parts = jwt.Split('.');
            if (parts.Length != 3) return null;
            var payload = parts[1];
            // Pad para base64url
            payload = payload.PadRight(payload.Length + (4 - payload.Length % 4) % 4, '=')
                            .Replace('-', '+').Replace('_', '/');
            var bytes = Convert.FromBase64String(payload);
            using var doc = System.Text.Json.JsonDocument.Parse(bytes);
            return doc.RootElement.TryGetProperty("sub", out var subProp)
                ? subProp.GetString() : null;
        }
        catch
        {
            return null;
        }
    }
}

// =============================================================================
// ValidateMfa (POST /api/v1/auth/mfa)
// =============================================================================
public static class ValidateMfa
{
    private const int MaxAttempts = 3;

    public sealed record Command(Guid SessionId, string Code);

    public sealed record Response(AuthTokens Tokens, AuthUserSnapshot User);

    public abstract record ValidateMfaError(string Code, string Message)
    {
        public sealed record SessionExpired() : ValidateMfaError("SESSION_EXPIRED", "La sesion MFA expiro, vuelve a iniciar sesion");
        public sealed record TooManyAttempts() : ValidateMfaError("TOO_MANY_ATTEMPTS", "Demasiados intentos fallidos, vuelve a iniciar sesion");
        public sealed record InvalidCode() : ValidateMfaError("INVALID_CODE", "Codigo MFA invalido");
        public sealed record UserNotFound() : ValidateMfaError("USER_NOT_FOUND", "Usuario no encontrado");
        public sealed record MfaNotEnabled() : ValidateMfaError("MFA_NOT_ENABLED", "MFA no esta habilitado para este usuario");
    }

    public static async Task<Result<Response, ValidateMfaError>> HandleAsync(
        Command cmd,
        IMfaSessionStore mfaStore,
        IUsersRepository users,
        ICognitoDirectory cognito,
        Aes256GcmSecretCipher cipher,
        TotpService totp,
        IClock clock,
        CancellationToken ct = default)
    {
        // 1. Recuperar sesion pendiente
        var session = await mfaStore.GetAsync(cmd.SessionId, ct);
        if (session is null)
            return Result<Response, ValidateMfaError>.Failure(new ValidateMfaError.SessionExpired());

        // 2. Validar max intentos
        if (session.Attempts >= MaxAttempts)
        {
            await mfaStore.DeleteAsync(cmd.SessionId, ct);
            await cognito.AdminUserGlobalSignOutAsync(session.Email, ct);
            return Result<Response, ValidateMfaError>.Failure(new ValidateMfaError.TooManyAttempts());
        }

        // 3. Cargar user con su mfa_secret cifrado
        var user = await users.GetByIdAsync(session.UserId, ct);
        if (user is null)
        {
            await mfaStore.DeleteAsync(cmd.SessionId, ct);
            return Result<Response, ValidateMfaError>.Failure(new ValidateMfaError.UserNotFound());
        }
        if (!user.MfaEnabled || user.MfaSecret is null)
        {
            await mfaStore.DeleteAsync(cmd.SessionId, ct);
            return Result<Response, ValidateMfaError>.Failure(new ValidateMfaError.MfaNotEnabled());
        }

        // 4. Descifrar secret y validar codigo TOTP
        var secretBase32 = cipher.Decrypt(user.MfaSecret);
        if (!totp.Verify(secretBase32, cmd.Code, clock.UtcNow))
        {
            await mfaStore.IncrementAttemptsAsync(cmd.SessionId, ct);
            return Result<Response, ValidateMfaError>.Failure(new ValidateMfaError.InvalidCode());
        }

        // 5. Codigo OK: borrar sesion, registrar login, devolver tokens
        await mfaStore.DeleteAsync(cmd.SessionId, ct);
        user.RegisterLogin(clock.UtcNow);
        await users.UpdateAsync(user, ct);

        var tokens = session.Tokens;
        return Result<Response, ValidateMfaError>.Success(new Response(
            new AuthTokens(tokens.AccessToken, tokens.IdToken, tokens.RefreshToken ?? string.Empty, tokens.ExpiresIn),
            new AuthUserSnapshot(user.Id, user.Email, user.FullName)));
    }
}

// =============================================================================
// RefreshToken (POST /api/v1/auth/refresh)
// =============================================================================
public static class RefreshAuthTokens
{
    public sealed record Command(string RefreshToken);
    public sealed record Response(AuthTokens Tokens);

    public abstract record RefreshError(string Code, string Message)
    {
        public sealed record InvalidRefreshToken() : RefreshError("INVALID_REFRESH_TOKEN", "Refresh token invalido o expirado");
        public sealed record UserNotActive(string Status) : RefreshError("USER_NOT_ACTIVE", $"Usuario no activo (status={Status})");
    }

    public static async Task<Result<Response, RefreshError>> HandleAsync(
        Command cmd,
        IUsersRepository users,
        ICognitoDirectory cognito,
        CancellationToken ct = default)
    {
        CognitoTokens tokens;
        try
        {
            tokens = await cognito.AdminRefreshAuthAsync(cmd.RefreshToken, ct);
        }
        catch
        {
            return Result<Response, RefreshError>.Failure(new RefreshError.InvalidRefreshToken());
        }

        // Validar que el user sigue ACTIVE en BD (defensa en profundidad)
        var sub = ExtractSubFromJwt(tokens.IdToken);
        if (sub is not null)
        {
            var user = await users.GetByCognitoSubAsync(sub, ct);
            if (user is not null && user.Status != UserStatus.ACTIVE)
            {
                await cognito.AdminUserGlobalSignOutAsync(user.Email, ct);
                return Result<Response, RefreshError>.Failure(
                    new RefreshError.UserNotActive(user.Status.ToString()));
            }
        }

        return Result<Response, RefreshError>.Success(new Response(
            new AuthTokens(tokens.AccessToken, tokens.IdToken, tokens.RefreshToken ?? cmd.RefreshToken, tokens.ExpiresIn)));
    }

    private static string? ExtractSubFromJwt(string jwt)
    {
        try
        {
            var parts = jwt.Split('.');
            if (parts.Length != 3) return null;
            var p = parts[1].PadRight(parts[1].Length + (4 - parts[1].Length % 4) % 4, '=')
                            .Replace('-', '+').Replace('_', '/');
            using var doc = System.Text.Json.JsonDocument.Parse(Convert.FromBase64String(p));
            return doc.RootElement.TryGetProperty("sub", out var s) ? s.GetString() : null;
        }
        catch { return null; }
    }
}

// =============================================================================
// Logout (POST /api/v1/auth/logout)
// =============================================================================
public static class Logout
{
    public sealed record Command(string RefreshToken);

    public static async Task<Result<Unit, string>> HandleAsync(
        Command cmd, ICognitoDirectory cognito, CancellationToken ct = default)
    {
        try
        {
            await cognito.RevokeTokenAsync(cmd.RefreshToken, ct);
            return Result<Unit, string>.Success(Unit.Value);
        }
        catch (Exception ex)
        {
            // Logout es best-effort. Si Cognito falla, el cliente igual debe borrar
            // el cookie. Devolvemos OK con audit (no error visible al usuario).
            return Result<Unit, string>.Failure($"Logout error (ignored): {ex.Message}");
        }
    }
}

// =============================================================================
// RequestPasswordReset (POST /api/v1/auth/password-reset/request)
// =============================================================================
public static class RequestPasswordReset
{
    public sealed record Command(string Email, string? IpAddress, string? UserAgent);
    public sealed record Response(); // Sin enumeracion: siempre devuelve 200

    /// <summary>
    /// Genera reset_token (32 bytes hex) + persiste hash SHA-256 con TTL 30min.
    /// Publica evento password_reset.requested al bus; el consumer de Notifications
    /// (in-process en core-api post ADR-0014) envia el email.
    /// NUNCA revela si el email existe o no (anti-enumeration).
    /// </summary>
    public static async Task<Response> HandleAsync(
        Command cmd,
        IUsersRepository users,
        IPasswordResetTokensRepository tokens,
        IClock clock,
        Action<string, string> onTokenGenerated, // callback para publicar al bus (delegado para no acoplar a RabbitMQ)
        CancellationToken ct = default)
    {
        var user = await users.GetByEmailAsync(cmd.Email, ct);
        if (user is null || user.Status != UserStatus.ACTIVE)
            return new Response(); // anti-enumeration

        // Generar token plaintext + hash SHA-256
        var rawToken = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
        var tokenHashBytes = SHA256.HashData(Encoding.ASCII.GetBytes(rawToken));
        var tokenHash = Convert.ToHexString(tokenHashBytes).ToLowerInvariant();

        var token = PasswordResetToken.Create(
            userId: user.Id,
            tokenHash: tokenHash,
            expiresAt: clock.UtcNow.AddMinutes(30),
            now: clock.UtcNow);

        await tokens.AddAsync(token, ct);

        // Notifica al caller (que publica al bus). Pasamos el plaintext porque
        // es la ultima vez que existe — luego solo queda el hash.
        onTokenGenerated(user.Email, rawToken);

        return new Response();
    }
}

// =============================================================================
// ConfirmPasswordReset (POST /api/v1/auth/password-reset/confirm)
// =============================================================================
public static class ConfirmPasswordReset
{
    public sealed record Command(string ResetToken, string NewPassword);

    public abstract record ConfirmError(string Code, string Message)
    {
        public sealed record InvalidToken() : ConfirmError("INVALID_TOKEN", "Token de reset invalido o expirado");
        public sealed record CognitoFailed(string Detail) : ConfirmError("COGNITO_FAILED", $"Cognito fallo: {Detail}");
    }

    public static async Task<Result<Unit, ConfirmError>> HandleAsync(
        Command cmd,
        IPasswordResetTokensRepository tokens,
        IUsersRepository users,
        ICognitoDirectory cognito,
        IClock clock,
        CancellationToken ct = default)
    {
        var hashBytes = SHA256.HashData(Encoding.ASCII.GetBytes(cmd.ResetToken));
        var hash = Convert.ToHexString(hashBytes).ToLowerInvariant();

        var token = await tokens.GetByHashAsync(hash, ct);
        if (token is null || token.UsedAt is not null || clock.UtcNow > token.ExpiresAt)
            return Result<Unit, ConfirmError>.Failure(new ConfirmError.InvalidToken());

        var user = await users.GetByIdAsync(token.UserId, ct);
        if (user is null)
            return Result<Unit, ConfirmError>.Failure(new ConfirmError.InvalidToken());

        try
        {
            await cognito.AdminSetUserPasswordAsync(user.Email, cmd.NewPassword, permanent: true, ct);
            await cognito.AdminUserGlobalSignOutAsync(user.Email, ct);
        }
        catch (Exception ex)
        {
            return Result<Unit, ConfirmError>.Failure(new ConfirmError.CognitoFailed(ex.Message));
        }

        token.MarkUsed(clock.UtcNow);
        await tokens.UpdateAsync(token, ct);
        return Result<Unit, ConfirmError>.Success(Unit.Value);
    }
}

// =============================================================================
// EnableMfa (POST /api/v1/auth/mfa/enable)
// =============================================================================
public static class EnableMfa
{
    public sealed record Command(Guid UserId);

    public sealed record Response(string SecretBase32, string OtpAuthUri);

    public abstract record EnableMfaError(string Code, string Message)
    {
        public sealed record UserNotFound() : EnableMfaError("USER_NOT_FOUND", "Usuario no encontrado");
        public sealed record UserNotActive() : EnableMfaError("USER_NOT_ACTIVE", "Usuario debe estar ACTIVE para habilitar MFA");
        public sealed record AlreadyEnabled() : EnableMfaError("MFA_ALREADY_ENABLED", "MFA ya esta habilitado");
    }

    /// <summary>
    /// Genera nuevo TOTP secret, lo cifra y lo persiste. Devuelve secret en base32
    /// + URI otpauth:// para que el frontend muestre el QR. El usuario debe
    /// confirmar con un codigo valido antes de cerrar el modal (no implementado
    /// aqui: el frontend valida con un POST /auth/mfa/verify-enrollment opcional).
    /// </summary>
    public static async Task<Result<Response, EnableMfaError>> HandleAsync(
        Command cmd,
        IUsersRepository users,
        Aes256GcmSecretCipher cipher,
        TotpService totp,
        string totpIssuer,
        IClock clock,
        CancellationToken ct = default)
    {
        var user = await users.GetByIdAsync(cmd.UserId, ct);
        if (user is null || user.DeletedAt is not null)
            return Result<Response, EnableMfaError>.Failure(new EnableMfaError.UserNotFound());
        if (user.Status != UserStatus.ACTIVE)
            return Result<Response, EnableMfaError>.Failure(new EnableMfaError.UserNotActive());
        if (user.MfaEnabled)
            return Result<Response, EnableMfaError>.Failure(new EnableMfaError.AlreadyEnabled());

        var secretBase32 = TotpService.GenerateSecret();
        var encrypted = cipher.Encrypt(secretBase32);

        user.EnableMfa(encrypted, clock.UtcNow);
        await users.UpdateAsync(user, ct);

        var uri = totp.BuildOtpAuthUri(secretBase32, totpIssuer, user.Email);
        return Result<Response, EnableMfaError>.Success(new Response(secretBase32, uri));
    }
}

// =============================================================================
// DisableMfa (POST /api/v1/auth/mfa/disable)
// =============================================================================
public static class DisableMfa
{
    public sealed record Command(Guid UserId);

    public static async Task<Result<Unit, string>> HandleAsync(
        Command cmd, IUsersRepository users, IClock clock, CancellationToken ct = default)
    {
        var user = await users.GetByIdAsync(cmd.UserId, ct);
        if (user is null || user.DeletedAt is not null)
            return Result<Unit, string>.Failure("USER_NOT_FOUND");

        user.DisableMfa(clock.UtcNow);
        await users.UpdateAsync(user, ct);
        return Result<Unit, string>.Success(Unit.Value);
    }
}

// =============================================================================
// IPasswordResetTokensRepository — port del modulo Auth
// =============================================================================
public interface IPasswordResetTokensRepository
{
    Task<PasswordResetToken?> GetByHashAsync(string tokenHash, CancellationToken ct = default);
    Task AddAsync(PasswordResetToken token, CancellationToken ct = default);
    Task UpdateAsync(PasswordResetToken token, CancellationToken ct = default);
}

// Unit reutilizado desde Flit.Modules.Users.Application (via using al top).
