using System.Data.Common;
using Flit.Modules.Companies.Ports;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Flit.Infrastructure.Persistence;

/// <summary>
/// Activa GUC de PostgreSQL para RLS (app.current_tenant_id, app.is_super_admin) en cada conexión.
/// </summary>
public sealed class TenantSessionConnectionInterceptor : DbConnectionInterceptor
{
    public override async Task ConnectionOpenedAsync(
        DbConnection connection,
        ConnectionEndEventData eventData,
        CancellationToken cancellationToken = default)
    {
        await TenantSessionGuc.ApplyAsync(connection, CompaniesSessionAmbient.Get(), cancellationToken);
        await base.ConnectionOpenedAsync(connection, eventData, cancellationToken);
    }

    public override void ConnectionOpened(
        DbConnection connection,
        ConnectionEndEventData eventData)
    {
        TenantSessionGuc.ApplyAsync(connection, CompaniesSessionAmbient.Get(), CancellationToken.None)
            .GetAwaiter()
            .GetResult();
        base.ConnectionOpened(connection, eventData);
    }
}
