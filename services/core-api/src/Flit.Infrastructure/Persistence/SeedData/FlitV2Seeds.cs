namespace Flit.Infrastructure.Persistence.SeedData;

/// <summary>
/// Seeds mínimos para el shell FLIT (post-reset Trámites 2.0).
/// Rol ADMIN, permisos de plataforma (users/rbac/menu/notifications) y menú base.
/// </summary>
public static class FlitV2Seeds
{
    private const string SeedTimestamp = "'2026-06-03T00:00:00Z'::timestamptz";

    public static class Roles
    {
        public const string Admin = "01900000-0001-7001-8001-000000000001";
    }

    public static class Perms
    {
        public const string UsersList = "01900000-0002-7001-8001-000000000001";
        public const string UsersCreate = "01900000-0002-7001-8001-000000000002";
        public const string UsersEdit = "01900000-0002-7001-8001-000000000003";
        public const string UsersChangeStatus = "01900000-0002-7001-8001-000000000004";
        public const string UsersDelete = "01900000-0002-7001-8001-000000000005";
        public const string UsersResetPassword = "01900000-0002-7001-8001-000000000006";
        public const string UsersManageMfa = "01900000-0002-7001-8001-000000000007";
        public const string RbacManageRoles = "01900000-0004-7001-8001-000000000001";
        public const string RbacManagePerms = "01900000-0004-7001-8001-000000000002";
        public const string RbacAssignRole = "01900000-0004-7001-8001-000000000003";
        public const string MenuManage = "01900000-0005-7001-8001-000000000001";
        public const string NotificationsSend = "01900000-0009-7001-8001-000000000001";
        public const string NotificationsView = "01900000-0009-7001-8001-000000000002";
    }

    public static class Menu
    {
        public const string Home = "01900000-000b-7001-8001-000000000001";
        public const string Administration = "01900000-000b-7001-8001-000000000003";
        public const string Profile = "01900000-000b-7001-8001-000000000005";
        public const string AdminUsers = "01900000-000b-7001-8001-000000000021";
        public const string AdminRoles = "01900000-000b-7001-8001-000000000022";
        public const string AdminPermissions = "01900000-000b-7001-8001-000000000023";
        public const string AdminMenus = "01900000-000b-7001-8001-000000000024";
    }

    public static async Task ApplyAllAsync(Func<string, Task> executeSqlAsync, CancellationToken ct = default)
    {
        await ApplyRoles(executeSqlAsync, ct);
        await ApplyPermissions(executeSqlAsync, ct);
        await ApplyMenuItems(executeSqlAsync, ct);
        await ApplyRolePermissions(executeSqlAsync, ct);
        await ApplyRoleMenuItems(executeSqlAsync, ct);
        await DemoAdmins.ApplyAsync(executeSqlAsync, ct);
    }

    private static Task ApplyRoles(Func<string, Task> executeSqlAsync, CancellationToken ct) =>
        executeSqlAsync($$"""
        INSERT INTO rbac.roles (id, code, name, description, is_system, is_active, created_at, updated_at) VALUES
            ('{{Roles.Admin}}', 'ADMIN', 'Administrador', 'Acceso total al shell FLIT', TRUE, TRUE, {{SeedTimestamp}}, {{SeedTimestamp}})
        ON CONFLICT (code) DO NOTHING;
        """);

    private static Task ApplyPermissions(Func<string, Task> executeSqlAsync, CancellationToken ct) =>
        executeSqlAsync($$"""
        INSERT INTO rbac.permissions (id, code, name, description, module, is_system, created_at) VALUES
            ('{{Perms.UsersList}}',          'USERS.LIST',            'Listar usuarios',        NULL, 'USERS', TRUE, {{SeedTimestamp}}),
            ('{{Perms.UsersCreate}}',        'USERS.CREATE',          'Crear usuarios',         NULL, 'USERS', TRUE, {{SeedTimestamp}}),
            ('{{Perms.UsersEdit}}',          'USERS.EDIT',            'Editar usuarios',        NULL, 'USERS', TRUE, {{SeedTimestamp}}),
            ('{{Perms.UsersChangeStatus}}',  'USERS.CHANGE_STATUS',   'Cambiar estado usuario', NULL, 'USERS', TRUE, {{SeedTimestamp}}),
            ('{{Perms.UsersDelete}}',        'USERS.DELETE',          'Eliminar usuario',       NULL, 'USERS', TRUE, {{SeedTimestamp}}),
            ('{{Perms.UsersResetPassword}}', 'USERS.RESET_PASSWORD',  'Resetear contraseña',    NULL, 'USERS', TRUE, {{SeedTimestamp}}),
            ('{{Perms.UsersManageMfa}}',     'USERS.MANAGE_MFA',      'Gestionar MFA',          NULL, 'USERS', TRUE, {{SeedTimestamp}}),
            ('{{Perms.RbacManageRoles}}',    'RBAC.MANAGE_ROLES',       'Gestionar roles',       NULL, 'RBAC', TRUE, {{SeedTimestamp}}),
            ('{{Perms.RbacManagePerms}}',    'RBAC.MANAGE_PERMISSIONS', 'Gestionar permisos',    NULL, 'RBAC', TRUE, {{SeedTimestamp}}),
            ('{{Perms.RbacAssignRole}}',     'RBAC.ASSIGN_ROLE',        'Asignar rol a usuario', NULL, 'RBAC', TRUE, {{SeedTimestamp}}),
            ('{{Perms.MenuManage}}',         'MENU.MANAGE',             'Gestionar menús',       NULL, 'MENU', TRUE, {{SeedTimestamp}}),
            ('{{Perms.NotificationsSend}}',  'NOTIFICATIONS.SEND',      'Enviar notificaciones', NULL, 'NOTIFICATIONS', TRUE, {{SeedTimestamp}}),
            ('{{Perms.NotificationsView}}',  'NOTIFICATIONS.VIEW_HISTORY', 'Ver historial de notificaciones', NULL, 'NOTIFICATIONS', TRUE, {{SeedTimestamp}})
        ON CONFLICT (code) DO NOTHING;
        """);

    private static Task ApplyMenuItems(Func<string, Task> executeSqlAsync, CancellationToken ct) =>
        executeSqlAsync($$"""
        INSERT INTO rbac.menu_items (id, code, parent_id, label, icon, frontend_path, sort_order, is_active, is_visible, is_separator, created_at) VALUES
            ('{{Menu.Home}}',           'HOME',           NULL, 'Inicio',          'home',         '/',                1, TRUE, TRUE, FALSE, {{SeedTimestamp}}),
            ('{{Menu.Administration}}', 'ADMINISTRATION', NULL, 'Administración',  'shield-check', NULL,               2, TRUE, TRUE, FALSE, {{SeedTimestamp}}),
            ('{{Menu.Profile}}',        'PROFILE',        NULL, 'Mi Perfil',       'user',         '/profile',        99, TRUE, TRUE, FALSE, {{SeedTimestamp}}),
            ('{{Menu.AdminUsers}}',       'ADMIN_USERS',       '{{Menu.Administration}}', 'Usuarios', 'users',  '/admin/users',       1, TRUE, TRUE, FALSE, {{SeedTimestamp}}),
            ('{{Menu.AdminRoles}}',       'ADMIN_ROLES',       '{{Menu.Administration}}', 'Roles',    'shield', '/admin/roles',       2, TRUE, TRUE, FALSE, {{SeedTimestamp}}),
            ('{{Menu.AdminPermissions}}', 'ADMIN_PERMISSIONS', '{{Menu.Administration}}', 'Permisos', 'key',    '/admin/permissions', 3, TRUE, TRUE, FALSE, {{SeedTimestamp}}),
            ('{{Menu.AdminMenus}}',       'ADMIN_MENUS',       '{{Menu.Administration}}', 'Menús',    'menu',   '/admin/menus',       4, TRUE, TRUE, FALSE, {{SeedTimestamp}})
        ON CONFLICT (code) DO NOTHING;
        """);

    private static Task ApplyRolePermissions(Func<string, Task> executeSqlAsync, CancellationToken ct) =>
        executeSqlAsync($$"""
        INSERT INTO rbac.role_permissions (role_id, permission_id, assigned_at, assigned_by_user_id)
        SELECT '{{Roles.Admin}}'::uuid, p.id, {{SeedTimestamp}}, NULL
        FROM rbac.permissions p
        WHERE p.is_system = TRUE
        ON CONFLICT (role_id, permission_id) DO NOTHING;
        """);

    private static Task ApplyRoleMenuItems(Func<string, Task> executeSqlAsync, CancellationToken ct) =>
        executeSqlAsync($$"""
        INSERT INTO rbac.role_menu_items (role_id, menu_item_id)
        SELECT '{{Roles.Admin}}'::uuid, mi.id
        FROM rbac.menu_items mi
        ON CONFLICT (role_id, menu_item_id) DO NOTHING;
        """);
}
