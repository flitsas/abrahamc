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

    public Task<ProcedureInstanceSearchResult> SearchAsync(
        Guid tenantId,
        int page,
        int pageSize,
        string? state,
        string? procedureTypeCode,
        CancellationToken ct = default)
    {
        var query = _store.Where(p => p.TenantId == tenantId);

        if (!string.IsNullOrWhiteSpace(state))
        {
            query = query.Where(p => string.Equals(p.State, state, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(procedureTypeCode))
        {
            var typeCode = procedureTypeCode.Trim();
            query = query.Where(p =>
                string.Equals(InferProcedureTypeCode(p), typeCode, StringComparison.OrdinalIgnoreCase));
        }

        var ordered = query.OrderByDescending(p => p.CreatedAt).ToList();
        var total = ordered.Count;
        var pageItems = ordered
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(MapSearchRow)
            .ToList();

        return Task.FromResult(new ProcedureInstanceSearchResult(pageItems, total));
    }

    public Task<int> GetNextSequenceAsync(
        Guid tenantId,
        Guid procedureTypeId,
        Guid? trafficAgencyId,
        CancellationToken ct = default)
    {
        var count = _store.Count(p =>
            p.TenantId == tenantId
            && p.ProcedureTypeId == procedureTypeId
            && p.TrafficAgencyId == trafficAgencyId);
        return Task.FromResult(count + 1);
    }

    public Task<ProcedureReferenceContext> ResolveReferenceContextAsync(
        Guid tenantId,
        Guid? trafficAgencyId,
        string procedureTypeCode,
        CancellationToken ct = default)
    {
        var tenantCode = tenantId.ToString("N")[..2].ToUpperInvariant();
        var otCode = trafficAgencyId?.ToString("N")[..3].ToUpperInvariant() ?? "GEN";
        return Task.FromResult(new ProcedureReferenceContext(
            ProcedureReferenceFormatter.AbbreviateProcedureTypeCode(procedureTypeCode),
            tenantCode,
            otCode));
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

    private static ProcedureInstanceSearchRow MapSearchRow(ProcedureInstance instance)
    {
        var typeCode = InferProcedureTypeCode(instance);
        return new ProcedureInstanceSearchRow(
            instance.Id,
            instance.TenantId,
            instance.ReferenceNumber,
            instance.ReferenceNumber,
            typeCode,
            instance.State,
            instance.ProcedureTypeId,
            instance.TrafficAgencyId,
            instance.RadicatedAt,
            instance.CreatedAt,
            instance.CreatedBy);
    }

    private static string InferProcedureTypeCode(ProcedureInstance instance)
    {
        if (instance.ReferenceNumber.StartsWith("TRASP", StringComparison.OrdinalIgnoreCase))
        {
            return "TRA_ESTANDAR";
        }

        if (instance.ReferenceNumber.StartsWith("MATLE", StringComparison.OrdinalIgnoreCase))
        {
            return "MAT_LEASING";
        }

        return "TRA_ESTANDAR";
    }
}
