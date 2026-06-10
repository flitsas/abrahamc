namespace Flit.Infrastructure.Persistence.Joins;

/// <summary>
/// Join entities para las tablas N:M de RBAC. Viven en Infrastructure (no Domain)
/// porque su propósito es 100% mapeo persistencia y EF Core requiere clases
/// concretas para poder agregar columnas extra (AssignedAt, AssignedByUserId).
///
/// Tablas:
///   - rbac.user_roles (PK compuesta user_id + role_id)
///   - rbac.role_permissions (PK compuesta role_id + permission_id)
///   - rbac.role_menu_items (PK compuesta role_id + menu_item_id)
/// </summary>
public sealed class UserRoleEntity
{
    public Guid UserId { get; set; }
    public Guid RoleId { get; set; }
    public DateTimeOffset AssignedAt { get; set; }
    public Guid? AssignedByUserId { get; set; }
}

public sealed class RolePermissionEntity
{
    public Guid RoleId { get; set; }
    public Guid PermissionId { get; set; }
    public DateTimeOffset AssignedAt { get; set; }
    public Guid? AssignedByUserId { get; set; }
}

public sealed class RoleMenuItemEntity
{
    public Guid RoleId { get; set; }
    public Guid MenuItemId { get; set; }
}
