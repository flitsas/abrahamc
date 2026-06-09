namespace Flit.Modules.ProceduresConfig.Ports;

public sealed record DocumentTypeRecord(string Code, string? DefaultPersonKind);

public interface IDocumentTypesReadRepository
{
    Task<DocumentTypeRecord?> GetByCodeAsync(string code, CancellationToken ct = default);
}
