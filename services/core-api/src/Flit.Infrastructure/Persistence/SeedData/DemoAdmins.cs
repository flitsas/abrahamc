namespace Flit.Infrastructure.Persistence.SeedData;

/// <summary>
/// Seed idempotente de usuarios admin para entorno demo/dev.
/// Se ejecuta después de FlitV2Seeds (que ya creó el rol ADMIN).
/// </summary>
public static class DemoAdmins
{
    private const string SeedTs = "'2026-05-22T00:00:00Z'::timestamptz";
    private const string AdminRoleId = FlitV2Seeds.Roles.Admin;

    private sealed record DemoAdmin(string Id, string Email, string FullName);

    private static readonly DemoAdmin[] Admins =
    [
        new("01900000-100b-7001-8001-000000000001", "admin@flit.io",     "Administrador FLIT"),
        new("01900000-100b-7001-8001-000000000002", "admin@flitsas.io",  "Administrador FLIT SAS"),
    ];

    public static async Task ApplyAsync(Func<string, Task> executeSqlAsync, CancellationToken ct = default)
    {
        foreach (var a in Admins)
        {
            await executeSqlAsync($"""
            INSERT INTO identity.users
                (id, cognito_sub, email, full_name, document_type, document_number, phone,
                 status, mfa_enabled, created_at, updated_at)
            VALUES
                ('{a.Id}'::uuid,
                 'stub-{a.Email}',
                 '{a.Email}',
                 '{a.FullName}',
                 'CC',
                 '1000000000',
                 '3000000000',
                 'ACTIVE',
                 FALSE,
                 {SeedTs},
                 {SeedTs})
            ON CONFLICT (email) WHERE deleted_at IS NULL DO NOTHING;
            """);

            await executeSqlAsync($"""
            INSERT INTO rbac.user_roles (user_id, role_id, assigned_at)
            SELECT u.id, '{AdminRoleId}'::uuid, {SeedTs}
            FROM identity.users u
            WHERE u.email = '{a.Email}'
            ON CONFLICT (user_id, role_id) DO NOTHING;
            """);
        }
    }
}
