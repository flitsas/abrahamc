using Flit.Modules.IdentityVerification.Domain;
using Flit.Modules.IdentityVerification.Ports;

namespace Flit.Modules.IdentityVerification.Adapters;

/// <summary>Stub de catálogo para modo in-memory / tests unitarios.</summary>
public sealed class InMemoryIdSecureCatalogLookup : IIdSecureCatalogLookup
{
    public Task<Guid?> GetDocumentTypeIdByCodeAsync(string code, CancellationToken ct = default) =>
        string.Equals(code, "CC", StringComparison.OrdinalIgnoreCase)
            ? Task.FromResult<Guid?>(IdSecureCatalogDefaults.DefaultDocumentTypeId)
            : Task.FromResult<Guid?>(null);
}
