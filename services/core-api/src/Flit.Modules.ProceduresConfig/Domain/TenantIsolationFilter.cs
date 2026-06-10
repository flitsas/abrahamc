namespace Flit.Modules.ProceduresConfig.Domain;

public interface ITenantScopedRow
{
    Guid TenantId { get; }
}

/// <summary>Filtro en memoria que simula RLS por tenant (#9432).</summary>
public static class TenantIsolationFilter
{
    public static IReadOnlyList<T> Apply<T>(Guid tenantId, IEnumerable<T> rows)
        where T : ITenantScopedRow =>
        rows.Where(r => r.TenantId == tenantId).ToList();
}
