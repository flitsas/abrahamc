namespace Flit.Modules.IdentityVerification.Ports;

/// <summary>
/// Resuelve IDs de catálogos globales (sin tenant) para flujos IDSecure públicos.
/// </summary>
public interface IIdSecureCatalogLookup
{
    Task<Guid?> GetDocumentTypeIdByCodeAsync(string code, CancellationToken ct = default);
}
