using Flit.Modules.Procedures.Ports;

namespace Flit.Modules.Procedures.Adapters;

public sealed class InMemoryProcedureActorRepository : IProcedureActorRepository
{
    private readonly List<ProcedureActorEntry> _store = [];

    public Task UpsertAsync(ProcedureActorEntry entry, CancellationToken ct = default)
    {
        var idx = _store.FindIndex(a =>
            a.ProcedureInstanceId == entry.ProcedureInstanceId &&
            string.Equals(a.EdgeRole, entry.EdgeRole, StringComparison.OrdinalIgnoreCase));

        if (idx >= 0)
        {
            _store[idx] = entry;
        }
        else
        {
            _store.Add(entry);
        }

        return Task.CompletedTask;
    }

    public Task<ProcedureActorEntry?> GetByInstanceAndEdgeAsync(
        Guid tenantId,
        Guid procedureInstanceId,
        string edgeRole,
        CancellationToken ct = default)
    {
        var result = _store.FirstOrDefault(a =>
            a.TenantId == tenantId &&
            a.ProcedureInstanceId == procedureInstanceId &&
            string.Equals(a.EdgeRole, edgeRole, StringComparison.OrdinalIgnoreCase));
        return Task.FromResult(result);
    }

    public Task<IReadOnlyList<ProcedureActorEntry>> ListByInstanceAsync(
        Guid tenantId,
        Guid procedureInstanceId,
        CancellationToken ct = default)
    {
        IReadOnlyList<ProcedureActorEntry> result = _store
            .Where(a => a.TenantId == tenantId && a.ProcedureInstanceId == procedureInstanceId)
            .ToList();
        return Task.FromResult(result);
    }
}
