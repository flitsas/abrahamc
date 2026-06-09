using Flit.Infrastructure.MultiTenant;
using Flit.Modules.Identity.Application;
using Flit.Modules.Identity.Ports;

namespace Flit.Api.Auth;

/// <summary>Filtro de endpoint que exige un slug activo en BD (HU #9417).</summary>
public sealed class TramitesPermissionFilter(string requiredSlug) : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
    {
        var http = context.HttpContext;
        var tenantContext = http.RequestServices.GetRequiredService<ITenantContext>();
        var tokenIssuer = http.RequestServices.GetRequiredService<ITokenIssuer>();
        var verifier = http.RequestServices.GetRequiredService<ITramitesPermissionVerifier>();

        if (!tenantContext.UserId.HasValue || !tenantContext.TenantId.HasValue)
        {
            var access = AuthCookieWriter.ReadAccessFromRequest(http.Request);
            if (!string.IsNullOrWhiteSpace(access))
            {
                var subject = tokenIssuer.ValidateTramitesAccess(access);
                if (subject is not null)
                {
                    tenantContext.Apply(
                        subject.TenantId, subject.UserId, subject.IsSuperAdmin,
                        trafficAgencyId: null,
                        http.Items[Middleware.CorrelationIdMiddleware.ItemKey] as Guid?,
                        http.Connection.RemoteIpAddress?.ToString());
                }
            }
        }

        if (!tenantContext.UserId.HasValue || !tenantContext.TenantId.HasValue)
            return Results.Unauthorized();

        var allowed = await verifier.HasSlugAsync(
            tenantContext.UserId.Value,
            tenantContext.TenantId.Value,
            tenantContext.IsSuperAdmin,
            requiredSlug,
            http.RequestAborted);

        if (!allowed)
        {
            return Results.Json(
                new
                {
                    error = TramitesRbacUseCases.ForbiddenCode,
                    message = $"Permiso requerido: {requiredSlug}",
                    requiredSlug,
                },
                statusCode: StatusCodes.Status403Forbidden);
        }

        return await next(context);
    }
}

public static class TramitesPermissionEndpointExtensions
{
    public static RouteHandlerBuilder RequireTramitesPermission(
        this RouteHandlerBuilder builder,
        string slug) =>
        builder.AddEndpointFilter(new TramitesPermissionFilter(slug));
}
