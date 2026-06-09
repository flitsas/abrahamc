using Flit.Modules.Rbac.Domain;

namespace Flit.Modules.Rbac.Ports;

public interface IRolesRepository
{
    Task<Role?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Role?> GetByCodeAsync(string code, CancellationToken ct = default);
    Task<bool> CodeExistsAsync(string code, CancellationToken ct = default);
    Task AddAsync(Role role, CancellationToken ct = default);
    Task UpdateAsync(Role role, CancellationToken ct = default);
    Task<IReadOnlyList<Role>> ListAsync(bool includeInactive, CancellationToken ct = default);
    Task<IReadOnlyList<Role>> GetByUserAsync(Guid userId, CancellationToken ct = default);
}

public interface IPermissionsRepository
{
    Task<Permission?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Permission?> GetByCodeAsync(string code, CancellationToken ct = default);
    Task<bool> CodeExistsAsync(string code, CancellationToken ct = default);
    Task AddAsync(Permission permission, CancellationToken ct = default);
    Task<IReadOnlyList<Permission>> ListAsync(string? moduleFilter, CancellationToken ct = default);
    Task<IReadOnlyList<Permission>> GetByRoleAsync(Guid roleId, CancellationToken ct = default);
    /// <summary>Permisos efectivos del usuario (union de todos sus roles).</summary>
    Task<IReadOnlyList<string>> GetUserPermissionCodesAsync(Guid userId, CancellationToken ct = default);
}

public interface IMenuItemsRepository
{
    Task<MenuItem?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<MenuItem?> GetByCodeAsync(string code, CancellationToken ct = default);
    Task AddAsync(MenuItem menuItem, CancellationToken ct = default);
    Task UpdateAsync(MenuItem menuItem, CancellationToken ct = default);
    Task<IReadOnlyList<MenuItem>> ListAllAsync(CancellationToken ct = default);
    /// <summary>Items visibles para el usuario segun sus roles. Devuelve plano,
    /// el use case se encarga de construir el arbol.</summary>
    Task<IReadOnlyList<MenuItem>> GetVisibleForUserAsync(Guid userId, CancellationToken ct = default);
}

public interface IRoleAssignmentsRepository
{
    Task AssignRoleToUserAsync(Guid userId, Guid roleId, Guid? assignedByUserId, CancellationToken ct = default);
    Task RevokeRoleFromUserAsync(Guid userId, Guid roleId, CancellationToken ct = default);
    Task AssignPermissionToRoleAsync(Guid roleId, Guid permissionId, Guid? assignedByUserId, CancellationToken ct = default);
    Task RevokePermissionFromRoleAsync(Guid roleId, Guid permissionId, CancellationToken ct = default);
    Task AssignMenuItemToRoleAsync(Guid roleId, Guid menuItemId, CancellationToken ct = default);
    Task RevokeMenuItemFromRoleAsync(Guid roleId, Guid menuItemId, CancellationToken ct = default);
}

/// <summary>
/// Cache de permisos efectivos por usuario. Impl Redis con TTL 5min en prod;
/// InMemory en dev. Invalidacion al cambiar user_roles o role_permissions.
/// </summary>
public interface IPermissionsCache
{
    Task<IReadOnlyList<string>?> GetAsync(Guid userId, CancellationToken ct = default);
    Task SetAsync(Guid userId, IReadOnlyList<string> permissionCodes, CancellationToken ct = default);
    Task InvalidateAsync(Guid userId, CancellationToken ct = default);
    Task InvalidateAllAsync(CancellationToken ct = default);
}
