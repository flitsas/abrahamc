using Flit.Api.Auth;
using Flit.Infrastructure.MultiTenant;
using Flit.Modules.Identity.Application;
using Flit.Modules.Identity.Ports;

namespace Flit.Api.Endpoints;

/// <summary>Consolas Super Admin / Tenant Admin (HU #9419).</summary>
public static class TramitesAdminEndpoints
{
    public const string ManageUsersPermission = TramitesUsersEndpoints.ManageUsersPermission;
    public sealed record CreateTenantRequest(string Name, string Nit, string Slug, string? SettingsJson);

    public sealed record UpdateTenantRequest(string? Name, string? Status, string? SettingsJson);

    public sealed record CreateCollaboratorRequest(
        string Email,
        string FullName,
        string? Phone,
        Guid? RoleId,
        Guid? TenantId);

    public sealed record UpdateCollaboratorRequest(
        string? FullName,
        string? Phone,
        string? AccountState);

    public static void MapTramitesAdminEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/identity/admin")
            .WithTags("Identity - Admin Console");

        group.MapGet("/session-context", async (
            Guid? tenantId,
            ITenantContext tenant,
            ITokenIssuer tokenIssuer,
            HttpContext http,
            IIdentityAdminRepository admin,
            CancellationToken ct) =>
        {
            if (!TryEnsureAuthenticated(tenant, tokenIssuer, http))
                return Results.Unauthorized();

            var actor = ToActor(tenant);
            var result = await TramitesAdminUseCases.GetSessionContextAsync(
                actor, tenantId, admin, ct);

            return result.Match(
                ok => Results.Ok(new
                {
                    effectiveTenantId = ok.EffectiveTenantId,
                    tenantName = ok.TenantName,
                    isSuperAdmin = ok.IsSuperAdmin,
                    actorUserId = ok.ActorUserId,
                }),
                MapTenantScopedError);
        })
        .RequireTramitesPermission("modulo.companias.crud-total")
        .WithName("TramitesAdminSessionContext");

        var tenants = group.MapGroup("/tenants");

        tenants.MapGet("/", async (
            string? status,
            string? search,
            ITenantContext tenant,
            ITokenIssuer tokenIssuer,
            HttpContext http,
            IIdentityAdminRepository admin,
            CancellationToken ct) =>
        {
            if (!TryEnsureAuthenticated(tenant, tokenIssuer, http))
                return Results.Unauthorized();
            if (!tenant.IsSuperAdmin)
                return ForbiddenSuperAdminOnly();

            var result = await TramitesAdminUseCases.ListTenantsAsync(
                ToActor(tenant), status, search, admin, ct);

            return result.Match(
                ok => Results.Ok(ok),
                MapTenantScopedError);
        })
        .RequireTramitesPermission("modulo.companias.crud-total")
        .WithName("TramitesAdminListTenants");

        tenants.MapGet("/{id:guid}", async (
            Guid id,
            ITenantContext tenant,
            ITokenIssuer tokenIssuer,
            HttpContext http,
            IIdentityAdminRepository admin,
            CancellationToken ct) =>
        {
            if (!TryEnsureAuthenticated(tenant, tokenIssuer, http))
                return Results.Unauthorized();
            if (!tenant.IsSuperAdmin)
                return ForbiddenSuperAdminOnly();

            var result = await TramitesAdminUseCases.GetTenantAsync(ToActor(tenant), id, admin, ct);
            return result.Match(
                ok => Results.Ok(ok),
                code => code == TramitesAdminUseCases.TenantNotFoundCode
                    ? Results.NotFound(new { error = code })
                    : MapTenantScopedError(code));
        })
        .RequireTramitesPermission("modulo.companias.crud-total")
        .WithName("TramitesAdminGetTenant");

        tenants.MapPost("/", async (
            CreateTenantRequest req,
            ITenantContext tenant,
            ITokenIssuer tokenIssuer,
            HttpContext http,
            IIdentityAdminRepository admin,
            CancellationToken ct) =>
        {
            if (!TryEnsureAuthenticated(tenant, tokenIssuer, http))
                return Results.Unauthorized();
            if (!tenant.IsSuperAdmin)
                return ForbiddenSuperAdminOnly();

            var result = await TramitesAdminUseCases.CreateTenantAsync(
                ToActor(tenant),
                new TramitesAdminUseCases.CreateTenantCommand(
                    req.Name, req.Nit, req.Slug, req.SettingsJson),
                admin,
                ct);

            return result.Match(
                ok => Results.Created($"/api/v1/identity/admin/tenants/{ok.Id}", ok),
                MapTenantScopedError);
        })
        .RequireTramitesPermission("modulo.companias.crud-total")
        .WithName("TramitesAdminCreateTenant");

        tenants.MapPatch("/{id:guid}", async (
            Guid id,
            UpdateTenantRequest req,
            ITenantContext tenant,
            ITokenIssuer tokenIssuer,
            HttpContext http,
            IIdentityAdminRepository admin,
            CancellationToken ct) =>
        {
            if (!TryEnsureAuthenticated(tenant, tokenIssuer, http))
                return Results.Unauthorized();
            if (!tenant.IsSuperAdmin)
                return ForbiddenSuperAdminOnly();

            var result = await TramitesAdminUseCases.UpdateTenantAsync(
                ToActor(tenant),
                new TramitesAdminUseCases.UpdateTenantCommand(
                    id, req.Name, req.Status, req.SettingsJson),
                admin,
                ct);

            return result.Match(
                ok => Results.Ok(ok),
                code => code == TramitesAdminUseCases.TenantNotFoundCode
                    ? Results.NotFound(new { error = code })
                    : MapTenantScopedError(code));
        })
        .RequireTramitesPermission("modulo.companias.crud-total")
        .WithName("TramitesAdminUpdateTenant");

        group.MapGet("/users", async (
            Guid? tenantId,
            int? page,
            int? limit,
            string? search,
            ITenantContext tenant,
            ITokenIssuer tokenIssuer,
            HttpContext http,
            IIdentityAdminRepository admin,
            CancellationToken ct) =>
        {
            if (!TryEnsureAuthenticated(tenant, tokenIssuer, http))
                return Results.Unauthorized();

            var result = await TramitesAdminUseCases.ListCollaboratorsAsync(
                ToActor(tenant), tenantId, page ?? 1, limit ?? 20, search, admin, ct);

            return result.Match(
                ok => Results.Ok(new
                {
                    items = ok.Items,
                    total = ok.Total,
                    page = page ?? 1,
                    limit = limit ?? 20,
                }),
                MapTenantScopedError);
        })
        .RequireTramitesPermission(ManageUsersPermission)
        .WithName("TramitesAdminListUsers");

        group.MapGet("/users/{id:guid}", async (
            Guid id,
            Guid? tenantId,
            ITenantContext tenant,
            ITokenIssuer tokenIssuer,
            HttpContext http,
            IIdentityAdminRepository admin,
            CancellationToken ct) =>
        {
            if (!TryEnsureAuthenticated(tenant, tokenIssuer, http))
                return Results.Unauthorized();

            var result = await TramitesAdminUseCases.GetCollaboratorAsync(
                ToActor(tenant), tenantId, id, admin, ct);

            return result.Match(
                ok => Results.Ok(ok),
                code => code == TramitesAdminUseCases.UserNotFoundCode
                    ? Results.NotFound(new { error = code })
                    : MapTenantScopedError(code));
        })
        .RequireTramitesPermission(ManageUsersPermission)
        .WithName("TramitesAdminGetUser");

        group.MapPost("/users", async (
            CreateCollaboratorRequest req,
            Guid? tenantId,
            ITenantContext tenant,
            ITokenIssuer tokenIssuer,
            HttpContext http,
            IIdentityAdminRepository admin,
            CancellationToken ct) =>
        {
            if (!TryEnsureAuthenticated(tenant, tokenIssuer, http))
                return Results.Unauthorized();

            var result = await TramitesAdminUseCases.CreateCollaboratorAsync(
                ToActor(tenant),
                tenantId,
                new TramitesAdminUseCases.CreateCollaboratorCommand(
                    req.Email, req.FullName, req.Phone, req.RoleId, req.TenantId),
                admin,
                ct);

            return result.Match(
                ok => Results.Created($"/api/v1/identity/admin/users/{ok.Id}", ok),
                MapTenantScopedError);
        })
        .RequireTramitesPermission(ManageUsersPermission)
        .WithName("TramitesAdminCreateUser");

        group.MapPatch("/users/{id:guid}", async (
            Guid id,
            UpdateCollaboratorRequest req,
            Guid? tenantId,
            ITenantContext tenant,
            ITokenIssuer tokenIssuer,
            HttpContext http,
            IIdentityAdminRepository admin,
            CancellationToken ct) =>
        {
            if (!TryEnsureAuthenticated(tenant, tokenIssuer, http))
                return Results.Unauthorized();

            var result = await TramitesAdminUseCases.UpdateCollaboratorAsync(
                ToActor(tenant),
                tenantId,
                new TramitesAdminUseCases.UpdateCollaboratorCommand(
                    id, req.FullName, req.Phone, req.AccountState),
                admin,
                ct);

            return result.Match(
                ok => Results.Ok(ok),
                code => code == TramitesAdminUseCases.UserNotFoundCode
                    ? Results.NotFound(new { error = code })
                    : MapTenantScopedError(code));
        })
        .RequireTramitesPermission(ManageUsersPermission)
        .WithName("TramitesAdminUpdateUser");

        group.MapGet("/roles", async (
            Guid? tenantId,
            ITenantContext tenant,
            ITokenIssuer tokenIssuer,
            HttpContext http,
            IIdentityAdminRepository admin,
            CancellationToken ct) =>
        {
            if (!TryEnsureAuthenticated(tenant, tokenIssuer, http))
                return Results.Unauthorized();

            var result = await TramitesAdminUseCases.ListAssignableRolesAsync(
                ToActor(tenant), tenantId, admin, ct);

            return result.Match(
                ok => Results.Ok(ok),
                MapTenantScopedError);
        })
        .RequireTramitesPermission(ManageUsersPermission)
        .WithName("TramitesAdminListRoles");

        group.MapPut("/users/{userId:guid}/roles/{roleId:guid}", async (
            Guid userId,
            Guid roleId,
            Guid? tenantId,
            ITenantContext tenant,
            ITokenIssuer tokenIssuer,
            HttpContext http,
            IIdentityAdminRepository admin,
            CancellationToken ct) =>
        {
            if (!TryEnsureAuthenticated(tenant, tokenIssuer, http))
                return Results.Unauthorized();

            var result = await TramitesAdminUseCases.AssignRoleAsync(
                ToActor(tenant), tenantId, userId, roleId, admin, ct);

            return result.Match(
                _ => Results.NoContent(),
                MapTenantScopedError);
        })
        .RequireTramitesPermission(ManageUsersPermission)
        .WithName("TramitesAdminAssignRole");

        group.MapDelete("/users/{userId:guid}/roles/{roleId:guid}", async (
            Guid userId,
            Guid roleId,
            Guid? tenantId,
            ITenantContext tenant,
            ITokenIssuer tokenIssuer,
            HttpContext http,
            IIdentityAdminRepository admin,
            CancellationToken ct) =>
        {
            if (!TryEnsureAuthenticated(tenant, tokenIssuer, http))
                return Results.Unauthorized();

            var result = await TramitesAdminUseCases.RemoveRoleAsync(
                ToActor(tenant), tenantId, userId, roleId, admin, ct);

            return result.Match(
                _ => Results.NoContent(),
                code => code == TramitesAdminUseCases.RoleNotFoundCode
                    ? Results.NotFound(new { error = code })
                    : MapTenantScopedError(code));
        })
        .RequireTramitesPermission(ManageUsersPermission)
        .WithName("TramitesAdminRemoveRole");
    }

    private static TramitesAdminUseCases.ActorContext ToActor(ITenantContext tenant) =>
        new(tenant.UserId!.Value, tenant.TenantId, tenant.IsSuperAdmin);

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

    private static IResult ForbiddenSuperAdminOnly() =>
        Results.Json(
            new
            {
                error = TramitesAdminUseCases.ForbiddenSuperAdminOnlyCode,
                message = "Solo Super Administrador puede ejecutar esta operación.",
            },
            statusCode: StatusCodes.Status403Forbidden);

    private static IResult MapTenantScopedError(string code) => code switch
    {
        TramitesAdminContext.TenantMismatchCode => Results.Json(
            new { error = code, message = "El tenant no coincide con el contexto de la sesión." },
            statusCode: StatusCodes.Status403Forbidden),
        TramitesAdminContext.TenantContextRequiredCode => Results.BadRequest(new
        {
            error = code,
            message = "Debe indicar tenantId en la consulta (selector de compañía).",
        }),
        TramitesAdminUseCases.ForbiddenSuperAdminOnlyCode => ForbiddenSuperAdminOnly(),
        TramitesAdminUseCases.ForbiddenRoleCode => Results.Json(
            new { error = code, message = "No puede asignar este rol." },
            statusCode: StatusCodes.Status403Forbidden),
        TramitesAdminUseCases.EmailExistsCode => Results.Conflict(new
        {
            error = code,
            message = "Ya existe un usuario con ese correo.",
        }),
        TramitesAdminUseCases.SlugConflictCode => Results.Conflict(new
        {
            error = code,
            message = "El slug de compañía ya existe.",
        }),
        TramitesAdminUseCases.NitConflictCode => Results.Conflict(new
        {
            error = code,
            message = "El NIT ya está registrado.",
        }),
        TramitesAdminUseCases.InvalidSlugCode => Results.BadRequest(new
        {
            error = code,
            message = "Formato de slug inválido.",
        }),
        TramitesAdminUseCases.InvalidAccountStateCode => Results.BadRequest(new
        {
            error = code,
            message = "Estado de cuenta inválido.",
        }),
        "INVALID_EMAIL" => Results.BadRequest(new { error = code, message = "Correo inválido." }),
        _ => Results.BadRequest(new { error = code }),
    };
}
