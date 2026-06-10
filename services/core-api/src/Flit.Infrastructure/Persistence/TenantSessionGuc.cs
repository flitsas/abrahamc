using System.Data.Common;
using Flit.Modules.Companies.Ports;

namespace Flit.Infrastructure.Persistence;

/// <summary>
/// Aplica GUC de PostgreSQL (app.current_tenant_id, app.is_super_admin) para políticas RLS.
/// Usado por el interceptor EF y por repositorios con SQL raw (Npgsql).
/// </summary>
public static class TenantSessionGuc
{
    public static async Task ApplyAsync(
        DbConnection connection,
        ICompaniesSessionContext? session,
        CancellationToken ct = default)
    {
        if (session is null)
        {
            return;
        }

        await using var cmd = connection.CreateCommand();
        var super = session.IsSuperAdmin ? "true" : "false";
        cmd.CommandText = $"SELECT set_config('app.is_super_admin', '{super}', false);";
        await cmd.ExecuteNonQueryAsync(ct);

        if (session.TenantId is Guid tenantId)
        {
            await using var tenantCmd = connection.CreateCommand();
            tenantCmd.CommandText =
                $"SELECT set_config('app.current_tenant_id', '{tenantId:D}', false);";
            await tenantCmd.ExecuteNonQueryAsync(ct);
        }
        else
        {
            await using var clearCmd = connection.CreateCommand();
            clearCmd.CommandText = "SELECT set_config('app.current_tenant_id', '', false);";
            await clearCmd.ExecuteNonQueryAsync(ct);
        }
    }
}
