using System.Collections.Concurrent;
using Flit.Modules.Rbac.Domain;
using Flit.Modules.Rbac.Ports;

namespace Flit.Modules.Rbac.Adapters;

/// <summary>
/// Adapters InMemory para desarrollo local sin Postgres.
/// En Fase EF las implementaciones reales viven en Flit.Infrastructure.Repositories.
/// </summary>
public sealed class InMemoryRolesRepository : IRolesRepository
{
    private readonly ConcurrentDictionary<Guid, Role> _byId = new();

    public Task<Role?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => Task.FromResult(_byId.TryGetValue(id, out var role) ? role : null);

    public Task<Role?> GetByCodeAsync(string code, CancellationToken ct = default)
    {
        var normalized = code.Trim().ToUpperInvariant();
        return Task.FromResult(_byId.Values.FirstOrDefault(r => r.Code == normalized));
    }

    public Task<bool> CodeExistsAsync(string code, CancellationToken ct = default)
    {
        var normalized = code.Trim().ToUpperInvariant();
        return Task.FromResult(_byId.Values.Any(r => r.Code == normalized));
    }

    public Task AddAsync(Role role, CancellationToken ct = default)
    {
        _byId[role.Id] = role;
        return Task.CompletedTask;
    }

    public Task UpdateAsync(Role role, CancellationToken ct = default)
    {
        _byId[role.Id] = role;
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<Role>> ListAsync(bool includeInactive, CancellationToken ct = default)
    {
        IReadOnlyList<Role> roles = includeInactive
            ? [.. _byId.Values.OrderBy(r => r.Code)]
            : [.. _byId.Values.Where(r => r.IsActive).OrderBy(r => r.Code)];
        return Task.FromResult(roles);
    }

    public Task<IReadOnlyList<Role>> GetByUserAsync(Guid userId, CancellationToken ct = default)
    {
        // InMemory no rastrea assignments; el InMemoryRoleAssignmentsRepository lo hace.
        IReadOnlyList<Role> empty = [];
        return Task.FromResult(empty);
    }
}

public sealed class InMemoryPermissionsRepository : IPermissionsRepository
{
    private readonly ConcurrentDictionary<Guid, Permission> _byId = new();
    private readonly InMemoryRoleAssignmentsRepository _assignments;
    private readonly IRolesRepository _rolesRepo;

    public InMemoryPermissionsRepository(
        InMemoryRoleAssignmentsRepository assignments,
        IRolesRepository rolesRepo)
    {
        _assignments = assignments;
        _rolesRepo = rolesRepo;
    }

    public Task<Permission?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => Task.FromResult(_byId.TryGetValue(id, out var p) ? p : null);

    public Task<Permission?> GetByCodeAsync(string code, CancellationToken ct = default)
    {
        var normalized = code.Trim().ToUpperInvariant();
        return Task.FromResult(_byId.Values.FirstOrDefault(p => p.Code == normalized));
    }

    public Task<bool> CodeExistsAsync(string code, CancellationToken ct = default)
    {
        var normalized = code.Trim().ToUpperInvariant();
        return Task.FromResult(_byId.Values.Any(p => p.Code == normalized));
    }

    public Task AddAsync(Permission permission, CancellationToken ct = default)
    {
        _byId[permission.Id] = permission;
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<Permission>> ListAsync(string? moduleFilter, CancellationToken ct = default)
    {
        IReadOnlyList<Permission> result = string.IsNullOrEmpty(moduleFilter)
            ? [.. _byId.Values.OrderBy(p => p.Code)]
            : [.. _byId.Values
                .Where(p => string.Equals(p.Module, moduleFilter, StringComparison.OrdinalIgnoreCase))
                .OrderBy(p => p.Code)];
        return Task.FromResult(result);
    }

    public Task<IReadOnlyList<Permission>> GetByRoleAsync(Guid roleId, CancellationToken ct = default)
    {
        var permissionIds = _assignments.GetPermissionIdsForRole(roleId);
        IReadOnlyList<Permission> result =
            [.. permissionIds.Select(id => _byId.TryGetValue(id, out var p) ? p : null)
                .Where(p => p is not null)
                .Cast<Permission>()];
        return Task.FromResult(result);
    }

    public async Task<IReadOnlyList<string>> GetUserPermissionCodesAsync(Guid userId, CancellationToken ct = default)
    {
        var roleIds = _assignments.GetRoleIdsForUser(userId);
        var codes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var roleId in roleIds)
        {
            var role = await _rolesRepo.GetByIdAsync(roleId, ct);
            if (role is null || !role.IsActive) continue;
            foreach (var permId in _assignments.GetPermissionIdsForRole(roleId))
            {
                if (_byId.TryGetValue(permId, out var p))
                    codes.Add(p.Code);
            }
        }
        return [.. codes.OrderBy(c => c)];
    }
}

public sealed class InMemoryMenuItemsRepository : IMenuItemsRepository
{
    private readonly ConcurrentDictionary<Guid, MenuItem> _byId = new();
    private readonly InMemoryRoleAssignmentsRepository _assignments;

    public InMemoryMenuItemsRepository(InMemoryRoleAssignmentsRepository assignments)
        => _assignments = assignments;

    public Task<MenuItem?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => Task.FromResult(_byId.TryGetValue(id, out var mi) ? mi : null);

    public Task<MenuItem?> GetByCodeAsync(string code, CancellationToken ct = default)
    {
        var normalized = code.Trim().ToUpperInvariant();
        return Task.FromResult(_byId.Values.FirstOrDefault(mi => mi.Code == normalized));
    }

    public Task AddAsync(MenuItem menuItem, CancellationToken ct = default)
    {
        _byId[menuItem.Id] = menuItem;
        return Task.CompletedTask;
    }

    public Task UpdateAsync(MenuItem menuItem, CancellationToken ct = default)
    {
        _byId[menuItem.Id] = menuItem;
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<MenuItem>> ListAllAsync(CancellationToken ct = default)
    {
        IReadOnlyList<MenuItem> all = [.. _byId.Values.OrderBy(m => m.SortOrder).ThenBy(m => m.Label)];
        return Task.FromResult(all);
    }

    public Task<IReadOnlyList<MenuItem>> GetVisibleForUserAsync(Guid userId, CancellationToken ct = default)
    {
        var roleIds = _assignments.GetRoleIdsForUser(userId);
        var menuItemIds = new HashSet<Guid>();
        foreach (var roleId in roleIds)
        {
            foreach (var miId in _assignments.GetMenuItemIdsForRole(roleId))
                menuItemIds.Add(miId);
        }

        IReadOnlyList<MenuItem> visible =
            [.. _byId.Values.Where(mi => menuItemIds.Contains(mi.Id)).OrderBy(mi => mi.SortOrder)];
        return Task.FromResult(visible);
    }
}

public sealed class InMemoryRoleAssignmentsRepository : IRoleAssignmentsRepository
{
    // user_id -> set of role_ids
    private readonly ConcurrentDictionary<Guid, HashSet<Guid>> _userRoles = new();
    // role_id -> set of permission_ids
    private readonly ConcurrentDictionary<Guid, HashSet<Guid>> _rolePermissions = new();
    // role_id -> set of menu_item_ids
    private readonly ConcurrentDictionary<Guid, HashSet<Guid>> _roleMenuItems = new();
    private readonly object _lock = new();

    public Task AssignRoleToUserAsync(Guid userId, Guid roleId, Guid? assignedByUserId, CancellationToken ct = default)
    {
        lock (_lock)
        {
            if (!_userRoles.TryGetValue(userId, out var set))
            {
                set = [];
                _userRoles[userId] = set;
            }
            set.Add(roleId);
        }
        return Task.CompletedTask;
    }

    public Task RevokeRoleFromUserAsync(Guid userId, Guid roleId, CancellationToken ct = default)
    {
        lock (_lock)
        {
            if (_userRoles.TryGetValue(userId, out var set))
                set.Remove(roleId);
        }
        return Task.CompletedTask;
    }

    public Task AssignPermissionToRoleAsync(Guid roleId, Guid permissionId, Guid? assignedByUserId, CancellationToken ct = default)
    {
        lock (_lock)
        {
            if (!_rolePermissions.TryGetValue(roleId, out var set))
            {
                set = [];
                _rolePermissions[roleId] = set;
            }
            set.Add(permissionId);
        }
        return Task.CompletedTask;
    }

    public Task RevokePermissionFromRoleAsync(Guid roleId, Guid permissionId, CancellationToken ct = default)
    {
        lock (_lock)
        {
            if (_rolePermissions.TryGetValue(roleId, out var set))
                set.Remove(permissionId);
        }
        return Task.CompletedTask;
    }

    public Task AssignMenuItemToRoleAsync(Guid roleId, Guid menuItemId, CancellationToken ct = default)
    {
        lock (_lock)
        {
            if (!_roleMenuItems.TryGetValue(roleId, out var set))
            {
                set = [];
                _roleMenuItems[roleId] = set;
            }
            set.Add(menuItemId);
        }
        return Task.CompletedTask;
    }

    public Task RevokeMenuItemFromRoleAsync(Guid roleId, Guid menuItemId, CancellationToken ct = default)
    {
        lock (_lock)
        {
            if (_roleMenuItems.TryGetValue(roleId, out var set))
                set.Remove(menuItemId);
        }
        return Task.CompletedTask;
    }

    // ─── Helpers para los repos de Permissions/MenuItems InMemory ───
    internal IReadOnlySet<Guid> GetRoleIdsForUser(Guid userId)
        => _userRoles.TryGetValue(userId, out var set) ? set : new HashSet<Guid>();

    internal IReadOnlySet<Guid> GetPermissionIdsForRole(Guid roleId)
        => _rolePermissions.TryGetValue(roleId, out var set) ? set : new HashSet<Guid>();

    internal IReadOnlySet<Guid> GetMenuItemIdsForRole(Guid roleId)
        => _roleMenuItems.TryGetValue(roleId, out var set) ? set : new HashSet<Guid>();
}

public sealed class InMemoryPermissionsCache : IPermissionsCache
{
    private readonly ConcurrentDictionary<Guid, IReadOnlyList<string>> _cache = new();

    public Task<IReadOnlyList<string>?> GetAsync(Guid userId, CancellationToken ct = default)
        => Task.FromResult(_cache.TryGetValue(userId, out var perms) ? perms : null);

    public Task SetAsync(Guid userId, IReadOnlyList<string> permissionCodes, CancellationToken ct = default)
    {
        _cache[userId] = permissionCodes;
        return Task.CompletedTask;
    }

    public Task InvalidateAsync(Guid userId, CancellationToken ct = default)
    {
        _cache.TryRemove(userId, out _);
        return Task.CompletedTask;
    }

    public Task InvalidateAllAsync(CancellationToken ct = default)
    {
        _cache.Clear();
        return Task.CompletedTask;
    }
}
