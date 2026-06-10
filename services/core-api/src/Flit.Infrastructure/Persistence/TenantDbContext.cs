using System.Data.Common;
using Microsoft.EntityFrameworkCore;

namespace Flit.Infrastructure.Persistence;

/// <summary>Establece variables de sesión RLS (app.current_tenant_id) antes de consultas tenant-scoped.</summary>
public static class TenantDbContext
{
    public static async Task SetTenantContextAsync(
        DbConnection connection,
        Guid tenantId,
        CancellationToken ct = default)
    {
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT set_config('app.current_tenant_id', @tid, true)";
        var p = cmd.CreateParameter();
        p.ParameterName = "tid";
        p.Value = tenantId.ToString();
        cmd.Parameters.Add(p);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    /// <summary>Contexto para modificar parametrización global (RLS procedures_config exige super admin).</summary>
    public static async Task SetConfigAdminContextAsync(
        DbConnection connection,
        Guid tenantId,
        CancellationToken ct = default)
    {
        await SetTenantContextAsync(connection, tenantId, ct);
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT set_config('app.is_super_admin', 'true', true)";
        await cmd.ExecuteNonQueryAsync(ct);
    }
}
