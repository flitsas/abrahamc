namespace Flit.SharedKernel;

/// <summary>
/// Contexto de tenant por request (F0-03). Usado por TenantConnectionInterceptor
/// para setear app.current_tenant_id antes de queries con RLS.
/// </summary>
public interface ITenantContext
{
    Guid? TenantId { get; }

    void SetTenant(Guid tenantId);
}

/// <summary>Implementacion scoped; el middleware o el handler de eventos setea el tenant.</summary>
public sealed class AmbientTenantContext : ITenantContext
{
    public Guid? TenantId { get; private set; }

    public void SetTenant(Guid tenantId) => TenantId = tenantId;
}
