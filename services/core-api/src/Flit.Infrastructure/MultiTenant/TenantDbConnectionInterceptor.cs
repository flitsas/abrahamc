using System.Data.Common;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace Flit.Infrastructure.MultiTenant;

/// <summary>
/// Interceptor EF Core: al abrir conexión aplica GUCs desde <see cref="ITenantContext"/> del request.
/// HU #9414 — AC1.
/// </summary>
public sealed class TenantDbConnectionInterceptor(IHttpContextAccessor httpContextAccessor)
    : DbConnectionInterceptor
{
    public override async Task ConnectionOpenedAsync(
        DbConnection connection,
        ConnectionEndEventData eventData,
        CancellationToken cancellationToken = default)
    {
        var context = httpContextAccessor.HttpContext?.RequestServices.GetService<ITenantContext>();
        if (context is not null)
            await TenantGucApplicator.ApplyAsync(connection, context, cancellationToken).ConfigureAwait(false);

        await base.ConnectionOpenedAsync(connection, eventData, cancellationToken).ConfigureAwait(false);
    }
}
