namespace Flit.Infrastructure.Persistence;

/// <summary>
/// Entidad de persistencia para credenciales locales (password hash).
/// Tabla: identity.identity_credentials (ADR-0007).
/// No es aggregate root; pertenece al schema de Identity.
/// </summary>
public sealed class IdentityCredential
{
    public Guid UserId { get; set; }
    public string PasswordHash { get; set; } = string.Empty;
    public DateTimeOffset CambiadoEn { get; set; }
}
