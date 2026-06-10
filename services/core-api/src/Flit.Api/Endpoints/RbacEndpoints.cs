using Flit.Modules.Rbac.Application;
using Flit.Modules.Rbac.Domain;
using Flit.Modules.Rbac.Ports;
using Flit.SharedKernel;

namespace Flit.Api.Endpoints;

/// <summary>
/// REST endpoints para RBAC (ADR-0011).
/// Roles + Permissions + MenuItems + asignaciones.
/// Permisos pendientes Fase 7: [RequirePermission("RBAC.MANAGE_ROLES")], etc.
/// </summary>
public static class RbacEndpoints
{
    // ─── DTOs ──────────────────────────────────────────────────────────
    public sealed record CreateRoleRequest(string Code, string Name, string? Description);
    public sealed record UpdateRoleRequest(string Name, string? Description);
    public sealed record CreatePermissionRequest(string Code, string Name, string? Description, string Module);
    public sealed record CreateMenuItemRequest(
        string Code, Guid? ParentId, string Label, string? Icon,
        string? FrontendPath, short SortOrder, bool IsSeparator);
    public sealed record UpdateMenuItemRequest(
        string Label, string? Icon, string? FrontendPath, short SortOrder);

    public static void MapRbacEndpoints(this IEndpointRouteBuilder app)
    {
        MapRoles(app);
        MapPermissions(app);
        MapMenuItems(app);
        MapAssignments(app);
    }

    private static void MapRoles(IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/v1/roles").WithTags("RBAC - Roles");

        g.MapGet("/", async (
            bool? includeInactive, IRolesRepository repo, CancellationToken ct) =>
        {
            var roles = await repo.ListAsync(includeInactive ?? false, ct);
            return Results.Ok(roles.Select(MapRole));
        })
        .WithName("ListRoles");

        g.MapGet("/{id:guid}", async (
            Guid id, IRolesRepository repo, CancellationToken ct) =>
        {
            var role = await repo.GetByIdAsync(id, ct);
            return role is null ? Results.NotFound() : Results.Ok(MapRole(role));
        })
        .WithName("GetRole");

        g.MapPost("/", async (
            CreateRoleRequest req, IRolesRepository repo, IClock clock, CancellationToken ct) =>
        {
            if (await repo.CodeExistsAsync(req.Code, ct))
                return Results.Conflict(new { error = "ROLE_CODE_DUPLICATED", message = "Code ya existe" });

            var role = Role.Create(req.Code, req.Name, req.Description, isSystem: false, clock.UtcNow);
            await repo.AddAsync(role, ct);
            return Results.Created($"/api/v1/roles/{role.Id}", MapRole(role));
        })
        .WithName("CreateRole");

        g.MapPatch("/{id:guid}", async (
            Guid id, UpdateRoleRequest req, IRolesRepository repo, IClock clock, CancellationToken ct) =>
        {
            var role = await repo.GetByIdAsync(id, ct);
            if (role is null) return Results.NotFound();
            try
            {
                role.UpdateProfile(req.Name, req.Description, clock.UtcNow);
                await repo.UpdateAsync(role, ct);
                return Results.Ok(MapRole(role));
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { error = "INVALID_OPERATION", message = ex.Message });
            }
        })
        .WithName("UpdateRole");
    }

    private static void MapPermissions(IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/v1/permissions").WithTags("RBAC - Permissions");

        g.MapGet("/", async (
            string? module, IPermissionsRepository repo, CancellationToken ct) =>
        {
            var perms = await repo.ListAsync(module, ct);
            return Results.Ok(perms.Select(MapPermission));
        })
        .WithName("ListPermissions");

        g.MapGet("/{id:guid}", async (
            Guid id, IPermissionsRepository repo, CancellationToken ct) =>
        {
            var p = await repo.GetByIdAsync(id, ct);
            return p is null ? Results.NotFound() : Results.Ok(MapPermission(p));
        })
        .WithName("GetPermission");

        g.MapPost("/", async (
            CreatePermissionRequest req, IPermissionsRepository repo, IClock clock, CancellationToken ct) =>
        {
            if (await repo.CodeExistsAsync(req.Code, ct))
                return Results.Conflict(new { error = "PERMISSION_CODE_DUPLICATED", message = "Code ya existe" });

            try
            {
                var p = Permission.Create(req.Code, req.Name, req.Description, req.Module, isSystem: false, clock.UtcNow);
                await repo.AddAsync(p, ct);
                return Results.Created($"/api/v1/permissions/{p.Id}", MapPermission(p));
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { error = "VALIDATION_FAILED", message = ex.Message });
            }
        })
        .WithName("CreatePermission");
    }

    private static void MapMenuItems(IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/v1/menu-items").WithTags("RBAC - Menu Items");

        g.MapGet("/", async (IMenuItemsRepository repo, CancellationToken ct) =>
        {
            var items = await repo.ListAllAsync(ct);
            return Results.Ok(items.Select(MapMenuItem));
        })
        .WithName("ListMenuItems");

        g.MapPost("/", async (
            CreateMenuItemRequest req, IMenuItemsRepository repo, IClock clock, CancellationToken ct) =>
        {
            var mi = MenuItem.Create(req.Code, req.ParentId, req.Label, req.Icon, req.FrontendPath,
                req.SortOrder, req.IsSeparator, clock.UtcNow);
            await repo.AddAsync(mi, ct);
            return Results.Created($"/api/v1/menu-items/{mi.Id}", MapMenuItem(mi));
        })
        .WithName("CreateMenuItem");

        g.MapPatch("/{id:guid}", async (
            Guid id, UpdateMenuItemRequest req, IMenuItemsRepository repo, IClock clock, CancellationToken ct) =>
        {
            var mi = await repo.GetByIdAsync(id, ct);
            if (mi is null) return Results.NotFound();
            mi.Update(req.Label, req.Icon, req.FrontendPath, req.SortOrder, clock.UtcNow);
            await repo.UpdateAsync(mi, ct);
            return Results.Ok(MapMenuItem(mi));
        })
        .WithName("UpdateMenuItem");
    }

    private static void MapAssignments(IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/v1").WithTags("RBAC - Assignments");

        // Asignar rol a usuario
        g.MapPost("/users/{userId:guid}/roles/{roleId:guid}", async (
            Guid userId, Guid roleId,
            IRolesRepository rolesRepo, IRoleAssignmentsRepository assignmentsRepo,
            IPermissionsCache cache, IClock clock, CancellationToken ct) =>
        {
            var result = await AssignRoleToUser.HandleAsync(
                new AssignRoleToUser.Command(userId, roleId, AssignedByUserId: null),
                rolesRepo, assignmentsRepo, cache, clock, ct);

            return result.Match(
                ok => Results.Ok(ok),
                err => err switch
                {
                    AssignRoleToUser.AssignRoleError.RoleNotFound =>
                        Results.NotFound(new { error = err.Code, message = err.Message }),
                    _ => Results.BadRequest(new { error = err.Code, message = err.Message }),
                });
        })
        .WithName("AssignRoleToUser");

        g.MapDelete("/users/{userId:guid}/roles/{roleId:guid}", async (
            Guid userId, Guid roleId,
            IRoleAssignmentsRepository repo, IPermissionsCache cache, CancellationToken ct) =>
        {
            await repo.RevokeRoleFromUserAsync(userId, roleId, ct);
            await cache.InvalidateAsync(userId, ct);
            return Results.NoContent();
        })
        .WithName("RevokeRoleFromUser");

        // Asignar permission a rol
        g.MapPost("/roles/{roleId:guid}/permissions/{permissionId:guid}", async (
            Guid roleId, Guid permissionId,
            IRoleAssignmentsRepository repo, IPermissionsCache cache, CancellationToken ct) =>
        {
            await repo.AssignPermissionToRoleAsync(roleId, permissionId, assignedByUserId: null, ct);
            await cache.InvalidateAllAsync(ct);
            return Results.NoContent();
        })
        .WithName("AssignPermissionToRole");

        g.MapDelete("/roles/{roleId:guid}/permissions/{permissionId:guid}", async (
            Guid roleId, Guid permissionId,
            IRoleAssignmentsRepository repo, IPermissionsCache cache, CancellationToken ct) =>
        {
            await repo.RevokePermissionFromRoleAsync(roleId, permissionId, ct);
            await cache.InvalidateAllAsync(ct);
            return Results.NoContent();
        })
        .WithName("RevokePermissionFromRole");

        // Asignar menu item a rol
        g.MapPost("/roles/{roleId:guid}/menu-items/{menuItemId:guid}", async (
            Guid roleId, Guid menuItemId,
            IRoleAssignmentsRepository repo, CancellationToken ct) =>
        {
            await repo.AssignMenuItemToRoleAsync(roleId, menuItemId, ct);
            return Results.NoContent();
        })
        .WithName("AssignMenuItemToRole");

        g.MapDelete("/roles/{roleId:guid}/menu-items/{menuItemId:guid}", async (
            Guid roleId, Guid menuItemId,
            IRoleAssignmentsRepository repo, CancellationToken ct) =>
        {
            await repo.RevokeMenuItemFromRoleAsync(roleId, menuItemId, ct);
            return Results.NoContent();
        })
        .WithName("RevokeMenuItemFromRole");
    }

    // ─── DTOs de respuesta ─────────────────────────────────────────────
    public sealed record RoleDto(
        Guid Id, string Code, string Name, string? Description,
        bool IsSystem, bool IsActive,
        DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);

    public sealed record PermissionDto(
        Guid Id, string Code, string Name, string? Description, string Module, bool IsSystem,
        DateTimeOffset CreatedAt);

    public sealed record MenuItemDto(
        Guid Id, string Code, Guid? ParentId, string Label, string? Icon,
        string? FrontendPath, short SortOrder, bool IsActive, bool IsVisible, bool IsSeparator,
        DateTimeOffset CreatedAt);

    private static RoleDto MapRole(Role r) => new(
        r.Id, r.Code, r.Name, r.Description, r.IsSystem, r.IsActive, r.CreatedAt, r.UpdatedAt);

    private static PermissionDto MapPermission(Permission p) => new(
        p.Id, p.Code, p.Name, p.Description, p.Module, p.IsSystem, p.CreatedAt);

    private static MenuItemDto MapMenuItem(MenuItem m) => new(
        m.Id, m.Code, m.ParentId, m.Label, m.Icon, m.FrontendPath,
        m.SortOrder, m.IsActive, m.IsVisible, m.IsSeparator, m.CreatedAt);
}
