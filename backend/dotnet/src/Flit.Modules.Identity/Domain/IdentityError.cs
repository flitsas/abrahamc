namespace Flit.Modules.Identity.Domain;

/// <summary>
/// Errores tipados del modulo Identity (discriminated union via abstract record).
/// Mapeo a HTTP Problem Details RFC 7807 lo hace el endpoint.
/// ADR-0006 §"Resumen ejecutivo de A".
/// </summary>
public abstract record IdentityError(string Code, string Message)
{
    public sealed record EmailInvalido(string Detail)
        : IdentityError("identity.email_invalido", Detail);

    public sealed record DocumentoInvalido(string Detail)
        : IdentityError("identity.documento_invalido", Detail);

    public sealed record PasswordInvalido(string Detail)
        : IdentityError("identity.password_invalido", Detail);

    public sealed record NombreInvalido(string Detail)
        : IdentityError("identity.nombre_invalido", Detail);

    public sealed record ConsentimientoRequerido()
        : IdentityError("identity.consentimiento_requerido",
            "Habeas Data Ley 1581: debe otorgar consentimiento de datos personales.");

    public sealed record EmailDuplicado(string Email)
        : IdentityError("identity.email_duplicado", $"Email ya registrado: {Email}");

    public sealed record DocumentoDuplicado(string Documento)
        : IdentityError("identity.documento_duplicado", $"Documento ya registrado: {Documento}");

    public sealed record UsuarioNoEncontrado(Guid Id)
        : IdentityError("identity.usuario_no_encontrado", $"Usuario {Id} no existe");

    public sealed record CredencialesInvalidas()
        : IdentityError("identity.credenciales_invalidas", "Email o contrasena incorrectos");

    public sealed record CuentaBloqueada(DateTimeOffset Hasta)
        : IdentityError("identity.cuenta_bloqueada",
            $"Cuenta bloqueada por intentos fallidos hasta {Hasta:O}");

    public sealed record CuentaInactiva()
        : IdentityError("identity.cuenta_inactiva", "La cuenta esta desactivada");

    public sealed record RefreshTokenInvalido()
        : IdentityError("identity.refresh_invalido", "Refresh token invalido, expirado o revocado");

    public sealed record OcrCedulaFallido(string DetailCode, string DetailMessage)
        : IdentityError("identity.ocr_cedula_fallido",
            $"OCR cedula fallo ({DetailCode}): {DetailMessage}");

    public sealed record OcrCedulaNoCoincide(string DocumentoEsperado, string DocumentoExtraido)
        : IdentityError("identity.ocr_cedula_no_coincide",
            $"Numero documento en cedula ({DocumentoExtraido}) no coincide con el del formulario ({DocumentoEsperado})");
}
