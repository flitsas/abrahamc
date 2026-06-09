using Flit.Modules.Procedures.Ports;

namespace Flit.Modules.Procedures.Adapters;

public sealed class InMemoryProcedureVehicleRepository : IProcedureVehicleRepository
{
    private readonly List<ProcedureVehicleEntry> _store = [];

    public Task UpsertAsync(ProcedureVehicleEntry entry, CancellationToken ct = default)
    {
        var idx = _store.FindIndex(v => v.ProcedureInstanceId == entry.ProcedureInstanceId);
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

    public Task<ProcedureVehicleEntry?> GetByInstanceAsync(
        Guid tenantId,
        Guid procedureInstanceId,
        CancellationToken ct = default)
    {
        var result = _store.FirstOrDefault(v =>
            v.TenantId == tenantId && v.ProcedureInstanceId == procedureInstanceId);
        return Task.FromResult(result);
    }
}
