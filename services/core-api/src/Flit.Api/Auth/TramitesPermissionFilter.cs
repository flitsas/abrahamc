using Flit.Infrastructure.MultiTenant;
using Flit.Modules.Identity.Application;
using Flit.Modules.Identity.Domain;
using Flit.Modules.Identity.Ports;

namespace Flit.Api.Auth;

/// <summary>Filtro de endpoint que exige un slug activo en BD (HU #9417) + ABAC (#9684).</summary>
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

        // ABAC solo cuando el endpoint establece contexto explícito (recurso concreto).
        var abacContext = TramitesAbacHttpContext.GetAbacContext(http);

        var allowed = await verifier.HasSlugWithAbacAsync(
            tenantContext.UserId.Value,
            tenantContext.TenantId.Value,
            tenantContext.IsSuperAdmin,
            requiredSlug,
            abacContext,
            http.RequestAborted);

        if (!allowed)
        {
            return Results.Json(
                new
                {
                    error = abacContext?.ResourceOwnerUserId is not null &&
                            abacContext.ResourceOwnerUserId != tenantContext.UserId
                        ? TramitesAbacCodes.Denied
                        : TramitesRbacUseCases.ForbiddenCode,
                    message = $"Permiso requerido: {requiredSlug}",
                    requiredSlug,
                },
                statusCode: StatusCodes.Status403Forbidden);
        }

        return await next(context);
    }
}

public sealed class TramitesAnyPermissionFilter(string[] requiredSlugs) : IEndpointFilter
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

        var abacContext = TramitesAbacHttpContext.GetAbacContext(http);

        foreach (var slug in requiredSlugs)
        {
            var allowed = await verifier.HasSlugWithAbacAsync(
                tenantContext.UserId.Value,
                tenantContext.TenantId.Value,
                tenantContext.IsSuperAdmin,
                slug,
                abacContext,
                http.RequestAborted);

            if (allowed)
            {
                return await next(context);
            }
        }

        return Results.Json(
            new
            {
                error = TramitesRbacUseCases.ForbiddenCode,
                message = $"Se requiere alguno de: {string.Join(", ", requiredSlugs)}",
                requiredSlugs,
            },
            statusCode: StatusCodes.Status403Forbidden);
    }
}

public static class TramitesPermissionEndpointExtensions
{
    public static RouteHandlerBuilder RequireTramitesPermission(
        this RouteHandlerBuilder builder,
        string slug) =>
        builder.AddEndpointFilter(new TramitesPermissionFilter(slug));

    public static RouteHandlerBuilder RequireTramitesAnyPermission(
        this RouteHandlerBuilder builder,
        params string[] slugs) =>
        builder.AddEndpointFilter(new TramitesAnyPermissionFilter(slugs));
}
