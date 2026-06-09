namespace Flit.Modules.Identity.Ports;

/// <summary>
/// Repositorio de credenciales locales (passwordHash).
/// Tabla identity_credential (ADR-0006 schema).
/// </summary>
public interface ICredentialsRepository
{
    Task<string?> ObtenerPasswordHashAsync(Guid userId, CancellationToken ct);
    Task GuardarPasswordHashAsync(Guid userId, string passwordHash, DateTimeOffset cambiadoEn, CancellationToken ct);
    Task EliminarAsync(Guid userId, CancellationToken ct);
}
