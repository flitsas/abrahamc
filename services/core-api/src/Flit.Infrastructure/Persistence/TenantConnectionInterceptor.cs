using System.Data.Common;
using Flit.SharedKernel;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Flit.Infrastructure.Persistence;

/// <summary>
/// Setea app.current_tenant_id al abrir conexion (docs/data-access-conventions.md §4).
/// </summary>
public sealed class TenantConnectionInterceptor(ITenantContext tenantContext) : DbConnectionInterceptor
{
    public override async Task ConnectionOpenedAsync(
        DbConnection connection,
        ConnectionEndEventData eventData,
        CancellationToken cancellationToken = default)
    {
        if (tenantContext.TenantId is { } tenantId)
        {
            await using var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT set_config('app.current_tenant_id', @tenant, false)";
            var param = cmd.CreateParameter();
            param.ParameterName = "tenant";
            param.Value = tenantId.ToString();
            cmd.Parameters.Add(param);
            await cmd.ExecuteNonQueryAsync(cancellationToken);
        }

        await base.ConnectionOpenedAsync(connection, eventData, cancellationToken);
    }
}
