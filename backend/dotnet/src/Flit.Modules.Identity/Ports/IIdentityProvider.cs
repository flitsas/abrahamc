using Flit.Modules.Identity.Domain;
using Flit.SharedKernel;

namespace Flit.Modules.Identity.Ports;

/// <summary>
/// Port para verificar credenciales y crear cuentas en el proveedor de
/// identidad (ADR-0006 Provider Pattern).
///
/// Implementaciones:
/// - LocalIdentityProvider: usa <see cref="ICredentialsRepository"/> +
///   <see cref="IPasswordHasher"/> (Argon2id local).
/// - CognitoIdentityProvider: usa AWS SDK AdminInitiateAuth + AdminCreateUser
///   (ADR-0008 clarificacion 2026-05-21).
///
/// Importante: el token devuelto por el provider es INTERNO al provider.
/// core-api emite SU PROPIO JWT RS256 con los claims del dominio
/// (sub=usuario.Id, rol, organismo_id) — los tokens de Cognito nunca
/// llegan al frontend.
/// </summary>
public interface IIdentityProvider
{
    string Name { get; }

    /// <summary>
    /// Verifica credenciales contra el provider. Devuelve Success si el
    /// par email+password es válido en el backing store del provider.
    /// </summary>
    Task<Result<Unit, IdentityError>> VerifyCredentialsAsync(
        string email, string password, CancellationToken ct);

    /// <summary>
    /// Crea la credencial en el provider (cuando se registra un usuario).
    /// Para Local: hashea con Argon2id y guarda en identity_credential.
    /// Para Cognito: AdminCreateUser + AdminSetUserPassword permanente.
    /// </summary>
    Task<Result<Unit, IdentityError>> CreateCredentialAsync(
        Guid userId, string email, string password, CancellationToken ct);

    /// <summary>
    /// Elimina la credencial (rollback de Register si OCR cedula no coincide,
    /// o eliminación de usuario).
    /// </summary>
    Task<Result<Unit, IdentityError>> DeleteCredentialAsync(
        Guid userId, string email, CancellationToken ct);

    /// <summary>
    /// Notifica logout global al provider (opcional). Para Local es no-op
    /// (basta con denylist del refresh en Redis). Para Cognito hace
    /// AdminUserGlobalSignOut.
    /// </summary>
    Task SignOutAsync(string email, CancellationToken ct);
}
