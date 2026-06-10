using Flit.Infrastructure.MultiTenant;
using Microsoft.EntityFrameworkCore;

namespace Flit.Infrastructure.Persistence;

/// <summary>
/// Aplica GUCs de RLS (tenant / super-admin) en la conexión activa de EF.
/// Necesario tras lookup cross-tenant por token o session id en endpoints públicos.
/// </summary>
public sealed class PostgresRlsSession(FlitDbContext db, ITenantContext tenantContext)
{
    public Task EnableSuperAdminReadAsync(CancellationToken ct = default) =>
        db.Database.ExecuteSqlRawAsync(
            "SELECT set_config('app.is_super_admin', 'true', false)",
            ct);

    public Task DisableSuperAdminReadAsync(CancellationToken ct = default) =>
        db.Database.ExecuteSqlRawAsync(
            "SELECT set_config('app.is_super_admin', 'false', false)",
            ct);

    public Task ApplyCurrentTenantAsync(CancellationToken ct = default)
    {
        if (tenantContext.TenantId is not { } tenantId)
            return Task.CompletedTask;

        return db.Database.ExecuteSqlInterpolatedAsync(
            $"""
             SELECT set_config('app.current_tenant_id', {tenantId.ToString()}, false),
                    set_config('app.is_super_admin', 'false', false)
             """,
            ct);
    }

    public async Task BindTenantFromInvitationLookupAsync(Guid tenantId, CancellationToken ct = default)
    {
        tenantContext.Apply(
            tenantId,
            tenantContext.UserId,
            isSuperAdmin: false,
            tenantContext.TrafficAgencyId,
            tenantContext.RequestId,
            tenantContext.ClientIp);
        await ApplyCurrentTenantAsync(ct);
    }
}
