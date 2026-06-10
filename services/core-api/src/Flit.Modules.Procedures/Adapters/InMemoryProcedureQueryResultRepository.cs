using System.Collections.Concurrent;
using Flit.Modules.Procedures.Ports;

namespace Flit.Modules.Procedures.Adapters;

public sealed class InMemoryProcedureQueryResultRepository : IProcedureQueryResultRepository
{
    private readonly ConcurrentDictionary<string, ProcedureQueryResultRecord> _store = new();

    public Task UpsertAsync(ProcedureQueryResultRecord record, CancellationToken ct = default)
    {
        _store[BuildKey(record.ProcedureInstanceId, record.QueryConnectorCode, record.EdgeRole)] = record;
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<ProcedureQueryResultRecord>> ListByInstanceAsync(
        Guid tenantId,
        Guid procedureInstanceId,
        CancellationToken ct = default)
    {
        var list = _store.Values
            .Where(r => r.TenantId == tenantId && r.ProcedureInstanceId == procedureInstanceId)
            .OrderBy(r => r.QueryConnectorCode)
            .ToList();
        return Task.FromResult<IReadOnlyList<ProcedureQueryResultRecord>>(list);
    }

    private static string BuildKey(Guid instanceId, string connector, string? edgeRole) =>
        $"{instanceId:N}:{connector}:{edgeRole ?? ""}";
}
