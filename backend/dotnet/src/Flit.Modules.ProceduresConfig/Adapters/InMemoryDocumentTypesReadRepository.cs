using Flit.Modules.ProceduresConfig.Ports;

namespace Flit.Modules.ProceduresConfig.Adapters;

public sealed class InMemoryDocumentTypesReadRepository : IDocumentTypesReadRepository
{
    private static readonly Dictionary<string, DocumentTypeRecord> Catalog =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["CC"] = new("CC", "natural"),
            ["CE"] = new("CE", "natural"),
            ["NIT"] = new("NIT", "juridica"),
            ["TI"] = new("TI", "natural"),
        };

    public Task<DocumentTypeRecord?> GetByCodeAsync(string code, CancellationToken ct = default) =>
        Task.FromResult(Catalog.TryGetValue(code, out var doc) ? doc : null);
}
