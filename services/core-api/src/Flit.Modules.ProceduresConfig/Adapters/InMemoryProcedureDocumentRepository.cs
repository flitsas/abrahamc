using System.Collections.Concurrent;
using System.Text.Json;
using Flit.Modules.ProceduresConfig.Domain;
using Flit.Modules.ProceduresConfig.Ports;

namespace Flit.Modules.ProceduresConfig.Adapters;

public sealed class InMemoryProcedureDocumentRepository : IProcedureDocumentRepository
{
    private readonly ConcurrentDictionary<Guid, GeneratedProcedureDocumentRecord> _docs = new();

    public Task<GeneratedProcedureDocumentRecord> AddGeneratedAsync(
        Guid tenantId,
        Guid procedureInstanceId,
        Guid documentTypeId,
        Guid templateVersionId,
        Guid fileId,
        string dataSnapshotJson,
        Guid actorUserId,
        CancellationToken ct = default)
    {
        var id = Guid.NewGuid();
        using var snapshotDoc = JsonDocument.Parse(dataSnapshotJson);
        var record = new GeneratedProcedureDocumentRecord(
            id,
            tenantId,
            procedureInstanceId,
            documentTypeId,
            templateVersionId,
            fileId,
            snapshotDoc.RootElement.Clone(),
            Status: "generated");

        _docs[id] = record;
        return Task.FromResult(record);
    }

    public GeneratedProcedureDocumentRecord? GetById(Guid id) =>
        _docs.TryGetValue(id, out var d) ? d : null;

    public Task<IReadOnlyList<GeneratedProcedureDocumentRecord>> ListByInstanceAsync(
        Guid tenantId,
        Guid procedureInstanceId,
        CancellationToken ct = default)
    {
        var list = _docs.Values
            .Where(d => d.TenantId == tenantId && d.ProcedureInstanceId == procedureInstanceId)
            .ToList();
        return Task.FromResult<IReadOnlyList<GeneratedProcedureDocumentRecord>>(list);
    }
}
