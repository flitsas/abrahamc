using Flit.Infrastructure.Persistence;
using Flit.Infrastructure.Persistence.Joins;
using Flit.Modules.Rbac.Domain;
using Flit.Modules.Rbac.Ports;
using Microsoft.EntityFrameworkCore;

namespace Flit.Infrastructure.Repositories;

/// <summary>
/// EF Core repositories del modulo RBAC (ADR-0011).
/// Validados contra docs/sql/flit-v2-initial-schema.sql.
/// </summary>
public sealed class EfRolesRepository : IRolesRepository
{
    private readonly FlitDbContext _db;
    public EfRolesRepository(FlitDbContext db) => _db = db;

    public Task<Role?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => _db.Roles.FirstOrDefaultAsync(r => r.Id == id, ct);

    public Task<Role?> GetByCodeAsync(string code, CancellationToken ct = default)
    {
        var normalized = code.Trim().ToUpperInvariant();
        return _db.Roles.FirstOrDefaultAsync(r => r.Code == normalized, ct);
    }

    public Task<bool> CodeExistsAsync(string code, CancellationToken ct = default)
    {
        var normalized = code.Trim().ToUpperInvariant();
        return _db.Roles.AnyAsync(r => r.Code == normalized, ct);
    }

    public async Task AddAsync(Role role, CancellationToken ct = default)
        => await _db.Roles.AddAsync(role, ct);

    public Task UpdateAsync(Role role, CancellationToken ct = default)
    {
        _db.Roles.Update(role);
        return Task.CompletedTask;
    }

    public async Task<IReadOnlyList<Role>> ListAsync(bool includeInactive, CancellationToken ct = default)
    {
        var q = _db.Roles.AsNoTracking();
        if (!includeInactive) q = q.Where(r => r.IsActive);
        var list = await q.OrderBy(r => r.Code).ToListAsync(ct);
        return list;
    }

    public async Task<IReadOnlyList<Role>> GetByUserAsync(Guid userId, CancellationToken ct = default)
    {
        var list = await (
            from ur in _db.UserRoleAssignments.AsNoTracking()
            join r in _db.Roles.AsNoTracking() on ur.RoleId equals r.Id
            where ur.UserId == userId
            select r
        ).OrderBy(r => r.Code).ToListAsync(ct);
        return list;
    }
}

public sealed class EfPermissionsRepository : IPermissionsRepository
{
    private readonly FlitDbContext _db;
    public EfPermissionsRepository(FlitDbContext db) => _db = db;

    public Task<Permission?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => _db.Permissions.FirstOrDefaultAsync(p => p.Id == id, ct);

    public Task<Permission?> GetByCodeAsync(string code, CancellationToken ct = default)
    {
        var normalized = code.Trim().ToUpperInvariant();
        return _db.Permissions.FirstOrDefaultAsync(p => p.Code == normalized, ct);
    }

    public Task<bool> CodeExistsAsync(string code, CancellationToken ct = default)
    {
        var normalized = code.Trim().ToUpperInvariant();
        return _db.Permissions.AnyAsync(p => p.Code == normalized, ct);
    }

    public async Task AddAsync(Permission permission, CancellationToken ct = default)
        => await _db.Permissions.AddAsync(permission, ct);

    public async Task<IReadOnlyList<Permission>> ListAsync(string? moduleFilter, CancellationToken ct = default)
    {
        var q = _db.Permissions.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(moduleFilter))
        {
            var m = moduleFilter.Trim().ToUpperInvariant();
            q = q.Where(p => p.Module == m);
        }
        var list = await q.OrderBy(p => p.Code).ToListAsync(ct);
        return list;
    }

    public async Task<IReadOnlyList<Permission>> GetByRoleAsync(Guid roleId, CancellationToken ct = default)
    {
        var list = await (
            from rp in _db.RolePermissions.AsNoTracking()
            join p in _db.Permissions.AsNoTracking() on rp.PermissionId equals p.Id
            where rp.RoleId == roleId
            select p
        ).OrderBy(p => p.Code).ToListAsync(ct);
        return list;
    }

    public async Task<IReadOnlyList<string>> GetUserPermissionCodesAsync(Guid userId, CancellationToken ct = default)
    {
        // Union de codes via user_roles → role_permissions → permissions.
        // Filtra por rol activo (igual que InMemoryPermissionsRepository).
        var codes = await (
            from ur in _db.UserRoleAssignments.AsNoTracking()
            join r in _db.Roles.AsNoTracking() on ur.RoleId equals r.Id
            join rp in _db.RolePermissions.AsNoTracking() on r.Id equals rp.RoleId
            join p in _db.Permissions.AsNoTracking() on rp.PermissionId equals p.Id
            where ur.UserId == userId && r.IsActive
            select p.Code
        ).Distinct().OrderBy(c => c).ToListAsync(ct);
        return codes;
    }
}

public sealed class EfMenuItemsRepository : IMenuItemsRepository
{
    private readonly FlitDbContext _db;
    public EfMenuItemsRepository(FlitDbContext db) => _db = db;

    public Task<MenuItem?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => _db.MenuItems.FirstOrDefaultAsync(m => m.Id == id, ct);

    public Task<MenuItem?> GetByCodeAsync(string code, CancellationToken ct = default)
    {
        var normalized = code.Trim().ToUpperInvariant();
        return _db.MenuItems.FirstOrDefaultAsync(m => m.Code == normalized, ct);
    }

    public async Task AddAsync(MenuItem menuItem, CancellationToken ct = default)
        => await _db.MenuItems.AddAsync(menuItem, ct);

    public Task UpdateAsync(MenuItem menuItem, CancellationToken ct = default)
    {
        _db.MenuItems.Update(menuItem);
        return Task.CompletedTask;
    }

    public async Task<IReadOnlyList<MenuItem>> ListAllAsync(CancellationToken ct = default)
    {
        var list = await _db.MenuItems.AsNoTracking()
            .OrderBy(m => m.SortOrder).ThenBy(m => m.Label)
            .ToListAsync(ct);
        return list;
    }

    public async Task<IReadOnlyList<MenuItem>> GetVisibleForUserAsync(Guid userId, CancellationToken ct = default)
    {
        var list = await (
            from ur in _db.UserRoleAssignments.AsNoTracking()
            join r in _db.Roles.AsNoTracking() on ur.RoleId equals r.Id
            join rmi in _db.RoleMenuItems.AsNoTracking() on r.Id equals rmi.RoleId
            join mi in _db.MenuItems.AsNoTracking() on rmi.MenuItemId equals mi.Id
            where ur.UserId == userId && r.IsActive && mi.IsActive && mi.IsVisible
            select mi
        ).Distinct().OrderBy(m => m.SortOrder).ToListAsync(ct);
        return list;
    }
}

public sealed class EfRoleAssignmentsRepository : IRoleAssignmentsRepository
{
    private readonly FlitDbContext _db;
    public EfRoleAssignmentsRepository(FlitDbContext db) => _db = db;

    public async Task AssignRoleToUserAsync(
        Guid userId, Guid roleId, Guid? assignedByUserId, CancellationToken ct = default)
    {
        var exists = await _db.UserRoleAssignments
            .AnyAsync(ur => ur.UserId == userId && ur.RoleId == roleId, ct);
        if (exists) return; // idempotente

        await _db.UserRoleAssignments.AddAsync(new UserRoleEntity
        {
            UserId = userId,
            RoleId = roleId,
            AssignedAt = DateTimeOffset.UtcNow,
            AssignedByUserId = assignedByUserId,
        }, ct);
        await _db.SaveChangesAsync(ct);
    }

    public async Task RevokeRoleFromUserAsync(Guid userId, Guid roleId, CancellationToken ct = default)
    {
        var assignment = await _db.UserRoleAssignments
            .FirstOrDefaultAsync(ur => ur.UserId == userId && ur.RoleId == roleId, ct);
        if (assignment is null) return;
        _db.UserRoleAssignments.Remove(assignment);
        await _db.SaveChangesAsync(ct);
    }

    public async Task AssignPermissionToRoleAsync(
        Guid roleId, Guid permissionId, Guid? assignedByUserId, CancellationToken ct = default)
    {
        var exists = await _db.RolePermissions
            .AnyAsync(rp => rp.RoleId == roleId && rp.PermissionId == permissionId, ct);
        if (exists) return;

        await _db.RolePermissions.AddAsync(new RolePermissionEntity
        {
            RoleId = roleId,
            PermissionId = permissionId,
            AssignedAt = DateTimeOffset.UtcNow,
            AssignedByUserId = assignedByUserId,
        }, ct);
        await _db.SaveChangesAsync(ct);
    }

    public async Task RevokePermissionFromRoleAsync(Guid roleId, Guid permissionId, CancellationToken ct = default)
    {
        var assignment = await _db.RolePermissions
            .FirstOrDefaultAsync(rp => rp.RoleId == roleId && rp.PermissionId == permissionId, ct);
        if (assignment is null) return;
        _db.RolePermissions.Remove(assignment);
        await _db.SaveChangesAsync(ct);
    }

    public async Task AssignMenuItemToRoleAsync(Guid roleId, Guid menuItemId, CancellationToken ct = default)
    {
        var exists = await _db.RoleMenuItems
            .AnyAsync(rmi => rmi.RoleId == roleId && rmi.MenuItemId == menuItemId, ct);
        if (exists) return;

        await _db.RoleMenuItems.AddAsync(new RoleMenuItemEntity
        {
            RoleId = roleId,
            MenuItemId = menuItemId,
        }, ct);
        await _db.SaveChangesAsync(ct);
    }

    public async Task RevokeMenuItemFromRoleAsync(Guid roleId, Guid menuItemId, CancellationToken ct = default)
    {
        var assignment = await _db.RoleMenuItems
            .FirstOrDefaultAsync(rmi => rmi.RoleId == roleId && rmi.MenuItemId == menuItemId, ct);
        if (assignment is null) return;
        _db.RoleMenuItems.Remove(assignment);
        await _db.SaveChangesAsync(ct);
    }
}
