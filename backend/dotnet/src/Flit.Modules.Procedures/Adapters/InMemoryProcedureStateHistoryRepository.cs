using Flit.Modules.Procedures.Domain;
using Flit.Modules.Procedures.Ports;

namespace Flit.Modules.Procedures.Adapters;

public sealed class InMemoryProcedureStateHistoryRepository : IProcedureStateHistoryRepository
{
    private readonly List<ProcedureStateHistoryEntry> _store = [];

    public IReadOnlyList<ProcedureStateHistoryEntry> All => _store.AsReadOnly();

    public Task AppendAsync(ProcedureStateHistoryEntry entry, CancellationToken ct = default)
    {
        _store.Add(entry);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<ProcedureStateHistoryEntry>> ListByInstanceAsync(
        Guid tenantId,
        Guid procedureInstanceId,
        CancellationToken ct = default)
    {
        IReadOnlyList<ProcedureStateHistoryEntry> result = _store
            .Where(e => e.TenantId == tenantId && e.ProcedureInstanceId == procedureInstanceId)
            .OrderByDescending(e => e.ChangedAt)
            .ToList();
        return Task.FromResult(result);
    }
}
