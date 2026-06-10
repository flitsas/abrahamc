using Flit.Api.Auth;
using Flit.Infrastructure.MultiTenant;
using Flit.Modules.Identity.Application;
using Flit.Modules.Identity.Ports;

namespace Flit.Api.Endpoints;

/// <summary>RBAC Trámites 2.0 por slugs (HU #9417) — reemplaza rbac legacy.</summary>
public static class TramitesRbacEndpoints
{
    public sealed record CreatePermissionRequest(
        string Slug,
        string Module,
        string Action,
        string? Description);

    public sealed record SetPermissionActiveRequest(bool IsActive);

    public static void MapTramitesRbacEndpoints(this IEndpointRouteBuilder app)
    {
        var permissions = app.MapGroup("/api/v1/permissions").WithTags("RBAC - Permissions");

        permissions.MapGet("/me", async (
            ITenantContext tenant,
            ITokenIssuer tokenIssuer,
            HttpContext http,
            IIdentityRbacRepository rbac,
            CancellationToken ct) =>
        {
            if (!TryEnsureAuthenticated(tenant, tokenIssuer, http))
                return Results.Unauthorized();

            var slugs = await rbac.GetEffectiveSlugsAsync(
                tenant.UserId!.Value, tenant.TenantId!.Value, ct);

            return Results.Ok(new { permissions = slugs });
        })
        .WithName("TramitesPermissionsMe");

        var admin = app.MapGroup("/api/v1/identity/permissions")
            .WithTags("RBAC - Permissions Admin");

        admin.MapGet("/", async (
            ITenantContext tenant,
            ITokenIssuer tokenIssuer,
            HttpContext http,
            IIdentityRbacRepository rbac,
            CancellationToken ct) =>
        {
            if (!TryEnsureAuthenticated(tenant, tokenIssuer, http))
                return Results.Unauthorized();
            if (!tenant.IsSuperAdmin)
                return Forbidden();

            var list = await rbac.ListPermissionsAsync(ct);
            return Results.Ok(list);
        })
        .WithName("TramitesListPermissions");

        admin.MapPost("/", async (
            CreatePermissionRequest req,
            ITenantContext tenant,
            ITokenIssuer tokenIssuer,
            HttpContext http,
            IIdentityRbacRepository rbac,
            CancellationToken ct) =>
        {
            if (!TryEnsureAuthenticated(tenant, tokenIssuer, http))
                return Results.Unauthorized();
            if (!tenant.IsSuperAdmin)
                return Forbidden();

            var result = await TramitesRbacUseCases.CreatePermissionAsync(
                new TramitesRbacUseCases.CreatePermissionCommand(
                    req.Slug, req.Module, req.Action, req.Description),
                rbac,
                tenant.UserId,
                ct);

            return result.Match(
                ok => Results.Created($"/api/v1/identity/permissions/{ok.Id}", ok),
                code => code switch
                {
                    TramitesRbacUseCases.SlugFormatCode => Results.BadRequest(new { error = code, message = "Formato de slug inválido." }),
                    TramitesRbacUseCases.SlugExistsCode => Results.Conflict(new { error = code, message = "El slug ya existe." }),
                    _ => Results.BadRequest(new { error = code }),
                });
        })
        .WithName("TramitesCreatePermission");

        admin.MapPatch("/{id:guid}/active", async (
            Guid id,
            SetPermissionActiveRequest req,
            ITenantContext tenant,
            ITokenIssuer tokenIssuer,
            HttpContext http,
            IIdentityRbacRepository rbac,
            CancellationToken ct) =>
        {
            if (!TryEnsureAuthenticated(tenant, tokenIssuer, http))
                return Results.Unauthorized();
            if (!tenant.IsSuperAdmin)
                return Forbidden();

            var result = await TramitesRbacUseCases.SetActiveAsync(id, req.IsActive, rbac, ct);
            return result.Match(
                ok => Results.Ok(ok),
                code => Results.NotFound(new { error = code, message = "Permiso no encontrado." }));
        })
        .WithName("TramitesTogglePermission");

        app.MapGet("/api/v1/tramites/protected-demo", () =>
            Results.Ok(new { message = "Acceso concedido al recurso protegido de trámites." }))
        .RequireTramitesPermission("modulo.tramites.ver")
        .WithName("TramitesProtectedDemo")
        .WithTags("RBAC - Demo");
    }

    private static bool TryEnsureAuthenticated(
        ITenantContext tenant,
        ITokenIssuer tokenIssuer,
        HttpContext http)
    {
        if (tenant.UserId.HasValue && tenant.TenantId.HasValue)
            return true;

        var access = AuthCookieWriter.ReadAccessFromRequest(http.Request);
        if (string.IsNullOrWhiteSpace(access))
            return false;

        var subject = tokenIssuer.ValidateTramitesAccess(access);
        if (subject is null)
            return false;

        tenant.Apply(
            subject.TenantId, subject.UserId, subject.IsSuperAdmin,
            trafficAgencyId: null,
            http.Items[Middleware.CorrelationIdMiddleware.ItemKey] as Guid?,
            http.Connection.RemoteIpAddress?.ToString());

        return true;
    }

    private static IResult Forbidden() =>
        Results.Json(
            new { error = TramitesRbacUseCases.ForbiddenCode, message = "Acceso denegado." },
            statusCode: StatusCodes.Status403Forbidden);
}
