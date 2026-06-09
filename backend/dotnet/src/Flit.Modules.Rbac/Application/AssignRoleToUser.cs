using Flit.Modules.Rbac.Ports;
using Flit.SharedKernel;

namespace Flit.Modules.Rbac.Application;

/// <summary>
/// Asigna un rol a un usuario. Invalida cache de permisos del usuario.
/// </summary>
public static class AssignRoleToUser
{
    public sealed record Command(Guid UserId, Guid RoleId, Guid? AssignedByUserId);

    public sealed record Response(Guid UserId, Guid RoleId, DateTimeOffset AssignedAt);

    public abstract record AssignRoleError(string Code, string Message)
    {
        public sealed record RoleNotFound()
            : AssignRoleError("ROLE_NOT_FOUND", "El rol no existe");

        public sealed record RoleInactive()
            : AssignRoleError("ROLE_INACTIVE", "El rol esta inactivo");
    }

    public static async Task<Result<Response, AssignRoleError>> HandleAsync(
        Command cmd,
        IRolesRepository rolesRepo,
        IRoleAssignmentsRepository assignmentsRepo,
        IPermissionsCache cache,
        IClock clock,
        CancellationToken ct = default)
    {
        var role = await rolesRepo.GetByIdAsync(cmd.RoleId, ct);
        if (role is null)
            return Result<Response, AssignRoleError>.Failure(
                new AssignRoleError.RoleNotFound());
        if (!role.IsActive)
            return Result<Response, AssignRoleError>.Failure(
                new AssignRoleError.RoleInactive());

        var now = clock.UtcNow;
        await assignmentsRepo.AssignRoleToUserAsync(
            cmd.UserId, cmd.RoleId, cmd.AssignedByUserId, ct);

        // Invalida cache para que el siguiente request recalcule permisos
        await cache.InvalidateAsync(cmd.UserId, ct);

        return Result<Response, AssignRoleError>.Success(
            new Response(cmd.UserId, cmd.RoleId, now));
    }
}
