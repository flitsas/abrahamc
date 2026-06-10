using Flit.Modules.Procedures.Domain;

namespace Flit.Modules.Procedures.Adapters;

/// <summary>
/// Repositorio en memoria para tests y modo sin Postgres.
/// Simula RLS: cada query filtra por <c>TenantId</c>.
/// </summary>
public sealed class InMemoryProcedureInstanceRepository : IProcedureInstanceRepository
{
    private readonly List<ProcedureInstance> _store = [];

    public IReadOnlyList<ProcedureInstance> All => _store.AsReadOnly();

    public Task<ProcedureInstance?> GetByIdAsync(Guid id, Guid tenantId, CancellationToken ct = default)
    {
        var result = _store.FirstOrDefault(p => p.Id == id && p.TenantId == tenantId);
        return Task.FromResult(result);
    }

    public Task<IReadOnlyList<ProcedureInstance>> ListByTenantAsync(
        Guid tenantId, CancellationToken ct = default)
    {
        IReadOnlyList<ProcedureInstance> result = _store
            .Where(p => p.TenantId == tenantId)
            .OrderByDescending(p => p.CreatedAt)
            .ToList();
        return Task.FromResult(result);
    }

    public Task SaveAsync(ProcedureInstance instance, CancellationToken ct = default)
    {
        var index = _store.FindIndex(p => p.Id == instance.Id);
        if (index >= 0)
        {
            _store[index] = instance;
        }
        else
        {
            _store.Add(instance);
        }

        return Task.CompletedTask;
    }
}
