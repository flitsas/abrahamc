using Flit.Infrastructure.Persistence;
using Flit.Modules.Identity.Domain;
using Flit.Modules.Identity.Ports;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Flit.Infrastructure.Repositories;

public sealed class NpgsqlIdentityAdminRepository(FlitDbContext db) : IIdentityAdminRepository
{
    public Task ApplySessionGucAsync(
        Guid tenantId,
        Guid userId,
        bool isSuperAdmin,
        CancellationToken ct = default) =>
        IdentitySessionGucApplicator.ApplyAsync(db, tenantId, userId, isSuperAdmin, ct);

    public async Task<IReadOnlyList<TenantRow>> ListTenantsAsync(
        string? status,
        string? search,
        CancellationToken ct = default)
    {
        var conn = await GetOpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(
            """
            SELECT id, name, nit, slug, status, settings::text, row_version
            FROM identity.tenants
            WHERE deleted_at IS NULL
              AND (@status::text IS NULL OR status = @status::text)
              AND (
                @search::text IS NULL
                OR name ILIKE '%' || @search::text || '%'
                OR nit ILIKE '%' || @search::text || '%'
                OR slug ILIKE '%' || @search::text || '%'
              )
            ORDER BY name
            """,
            conn);
        cmd.Parameters.AddWithValue("status", (object?)status ?? DBNull.Value);
        cmd.Parameters.AddWithValue("search", (object?)search ?? DBNull.Value);

        var rows = new List<TenantRow>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            rows.Add(MapTenant(reader));

        return rows;
    }

    public async Task<TenantRow?> GetTenantByIdAsync(Guid id, CancellationToken ct = default)
    {
        var conn = await GetOpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(
            """
            SELECT id, name, nit, slug, status, settings::text, row_version
            FROM identity.tenants
            WHERE id = @id AND deleted_at IS NULL
            """,
            conn);
        cmd.Parameters.AddWithValue("id", id);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        return await reader.ReadAsync(ct) ? MapTenant(reader) : null;
    }

    public async Task<string?> GetTenantNameAsync(Guid id, CancellationToken ct = default)
    {
        var conn = await GetOpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(
            "SELECT name FROM identity.tenants WHERE id = @id AND deleted_at IS NULL",
            conn);
        cmd.Parameters.AddWithValue("id", id);
        return (string?)await cmd.ExecuteScalarAsync(ct);
    }

    public async Task<TenantRow> CreateTenantAsync(
        string name,
        string nit,
        string slug,
        string? settingsJson,
        Guid createdBy,
        CancellationToken ct = default)
    {
        var id = Guid.CreateVersion7();
        var settings = settingsJson ?? "{}";
        var conn = await GetOpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(
            """
            INSERT INTO identity.tenants (id, name, nit, slug, status, settings, created_by, updated_by)
            VALUES (@id, @name, @nit, @slug, 'active', @settings::jsonb, @createdBy, @createdBy)
            RETURNING id, name, nit, slug, status, settings::text, row_version
            """,
            conn);
        cmd.Parameters.AddWithValue("id", id);
        cmd.Parameters.AddWithValue("name", name);
        cmd.Parameters.AddWithValue("nit", nit);
        cmd.Parameters.AddWithValue("slug", slug);
        cmd.Parameters.AddWithValue("settings", settings);
        cmd.Parameters.AddWithValue("createdBy", createdBy);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        await reader.ReadAsync(ct);
        return MapTenant(reader);
    }

    public async Task<TenantRow?> UpdateTenantAsync(
        Guid id,
        string? name,
        string? status,
        string? settingsJson,
        Guid updatedBy,
        CancellationToken ct = default)
    {
        var conn = await GetOpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(
            """
            UPDATE identity.tenants
            SET
              name = COALESCE(@name::text, name),
              status = COALESCE(@status::text, status),
              settings = COALESCE(@settings::jsonb, settings),
              updated_at = now(),
              updated_by = @updatedBy
            WHERE id = @id AND deleted_at IS NULL
            RETURNING id, name, nit, slug, status, settings::text, row_version
            """,
            conn);
        cmd.Parameters.AddWithValue("id", id);
        cmd.Parameters.AddWithValue("name", (object?)name ?? DBNull.Value);
        cmd.Parameters.AddWithValue("status", (object?)status ?? DBNull.Value);
        cmd.Parameters.AddWithValue("settings", (object?)settingsJson ?? DBNull.Value);
        cmd.Parameters.AddWithValue("updatedBy", updatedBy);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        return await reader.ReadAsync(ct) ? MapTenant(reader) : null;
    }

    public async Task<bool> TenantSlugExistsAsync(string slug, Guid? excludeId, CancellationToken ct = default)
    {
        var conn = await GetOpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(
            """
            SELECT EXISTS (
              SELECT 1 FROM identity.tenants
              WHERE slug = @slug AND deleted_at IS NULL
                AND (@excludeId::uuid IS NULL OR id <> @excludeId::uuid)
            )
            """,
            conn);
        cmd.Parameters.AddWithValue("slug", slug);
        cmd.Parameters.AddWithValue("excludeId", (object?)excludeId ?? DBNull.Value);
        return (bool)(await cmd.ExecuteScalarAsync(ct) ?? false);
    }

    public async Task<bool> TenantNitExistsAsync(string nit, Guid? excludeId, CancellationToken ct = default)
    {
        var conn = await GetOpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(
            """
            SELECT EXISTS (
              SELECT 1 FROM identity.tenants
              WHERE nit = @nit AND deleted_at IS NULL
                AND (@excludeId::uuid IS NULL OR id <> @excludeId::uuid)
            )
            """,
            conn);
        cmd.Parameters.AddWithValue("nit", nit);
        cmd.Parameters.AddWithValue("excludeId", (object?)excludeId ?? DBNull.Value);
        return (bool)(await cmd.ExecuteScalarAsync(ct) ?? false);
    }

    public async Task<bool> EmailExistsGloballyAsync(string email, CancellationToken ct = default)
    {
        var conn = await GetOpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(
            """
            SELECT EXISTS (
              SELECT 1 FROM identity.users
              WHERE email = @email::citext AND deleted_at IS NULL
            )
            """,
            conn);
        cmd.Parameters.AddWithValue("email", email);
        return (bool)(await cmd.ExecuteScalarAsync(ct) ?? false);
    }

    public async Task<(IReadOnlyList<CollaboratorRow> Items, int Total)> ListCollaboratorsAsync(
        Guid tenantId,
        int page,
        int limit,
        string? search,
        CancellationToken ct = default)
    {
        var offset = Math.Max(0, (page - 1) * limit);
        var conn = await GetOpenConnectionAsync(ct);

        await using var countCmd = new NpgsqlCommand(
            """
            SELECT COUNT(*)
            FROM identity.users u
            LEFT JOIN identity.profiles p ON p.user_id = u.id AND p.deleted_at IS NULL
            WHERE u.tenant_id = @tenantId AND u.deleted_at IS NULL
              AND (
                @search::text IS NULL
                OR u.email::text ILIKE '%' || @search::text || '%'
                OR p.full_name ILIKE '%' || @search::text || '%'
              )
            """,
            conn);
        countCmd.Parameters.AddWithValue("tenantId", tenantId);
        countCmd.Parameters.AddWithValue("search", (object?)search ?? DBNull.Value);
        var total = Convert.ToInt32(await countCmd.ExecuteScalarAsync(ct), System.Globalization.CultureInfo.InvariantCulture);

        await using var cmd = new NpgsqlCommand(
            """
            SELECT u.id, u.tenant_id, u.email::text, u.account_state, p.full_name, p.phone, u.row_version,
              COALESCE(
                (SELECT array_agg(r.slug ORDER BY r.slug)
                 FROM identity.user_roles ur
                 JOIN identity.roles r ON r.id = ur.role_id AND r.deleted_at IS NULL
                 WHERE ur.tenant_id = u.tenant_id AND ur.user_id = u.id AND ur.deleted_at IS NULL),
                ARRAY[]::text[]
              ) AS role_slugs
            FROM identity.users u
            LEFT JOIN identity.profiles p ON p.user_id = u.id AND p.deleted_at IS NULL
            WHERE u.tenant_id = @tenantId AND u.deleted_at IS NULL
              AND (
                @search::text IS NULL
                OR u.email::text ILIKE '%' || @search::text || '%'
                OR p.full_name ILIKE '%' || @search::text || '%'
              )
            ORDER BY u.email
            LIMIT @limit OFFSET @offset
            """,
            conn);
        cmd.Parameters.AddWithValue("tenantId", tenantId);
        cmd.Parameters.AddWithValue("search", (object?)search ?? DBNull.Value);
        cmd.Parameters.AddWithValue("limit", limit);
        cmd.Parameters.AddWithValue("offset", offset);

        var items = new List<CollaboratorRow>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            items.Add(new CollaboratorRow(
                reader.GetGuid(0),
                reader.GetGuid(1),
                reader.GetString(2),
                reader.GetString(3),
                reader.IsDBNull(4) ? null : reader.GetString(4),
                reader.IsDBNull(5) ? null : reader.GetString(5),
                reader.GetFieldValue<string[]>(7),
                reader.GetInt32(6)));
        }

        return (items, total);
    }

    public async Task<CollaboratorRow?> GetCollaboratorAsync(
        Guid tenantId,
        Guid userId,
        CancellationToken ct = default)
    {
        var conn = await GetOpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(
            """
            SELECT u.id, u.tenant_id, u.email::text, u.account_state, p.full_name, p.phone, u.row_version,
              COALESCE(
                (SELECT array_agg(r.slug ORDER BY r.slug)
                 FROM identity.user_roles ur
                 JOIN identity.roles r ON r.id = ur.role_id AND r.deleted_at IS NULL
                 WHERE ur.tenant_id = u.tenant_id AND ur.user_id = u.id AND ur.deleted_at IS NULL),
                ARRAY[]::text[]
              ) AS role_slugs
            FROM identity.users u
            LEFT JOIN identity.profiles p ON p.user_id = u.id AND p.deleted_at IS NULL
            WHERE u.tenant_id = @tenantId AND u.id = @userId AND u.deleted_at IS NULL
            """,
            conn);
        cmd.Parameters.AddWithValue("tenantId", tenantId);
        cmd.Parameters.AddWithValue("userId", userId);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
            return null;

        return new CollaboratorRow(
            reader.GetGuid(0),
            reader.GetGuid(1),
            reader.GetString(2),
            reader.GetString(3),
            reader.IsDBNull(4) ? null : reader.GetString(4),
            reader.IsDBNull(5) ? null : reader.GetString(5),
            reader.GetFieldValue<string[]>(7),
            reader.GetInt32(6));
    }

    public async Task<Guid> CreateCollaboratorUserAsync(
        Guid tenantId,
        string email,
        Guid createdBy,
        CancellationToken ct = default)
    {
        var id = Guid.CreateVersion7();
        var conn = await GetOpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(
            """
            INSERT INTO identity.users (
              id, tenant_id, email, password_hash, account_state, created_by, updated_by
            ) VALUES (
              @id, @tenantId, @email::citext, NULL, 'inactive', @createdBy, @createdBy
            )
            """,
            conn);
        cmd.Parameters.AddWithValue("id", id);
        cmd.Parameters.AddWithValue("tenantId", tenantId);
        cmd.Parameters.AddWithValue("email", email);
        cmd.Parameters.AddWithValue("createdBy", createdBy);
        await cmd.ExecuteNonQueryAsync(ct);
        return id;
    }

    public async Task CreateCollaboratorProfileAsync(
        Guid tenantId,
        Guid userId,
        string fullName,
        string? phone,
        Guid createdBy,
        CancellationToken ct = default)
    {
        var id = Guid.CreateVersion7();
        var conn = await GetOpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(
            """
            INSERT INTO identity.profiles (
              id, tenant_id, user_id, full_name, phone, created_by, updated_by
            ) VALUES (
              @id, @tenantId, @userId, @fullName, @phone, @createdBy, @createdBy
            )
            """,
            conn);
        cmd.Parameters.AddWithValue("id", id);
        cmd.Parameters.AddWithValue("tenantId", tenantId);
        cmd.Parameters.AddWithValue("userId", userId);
        cmd.Parameters.AddWithValue("fullName", fullName);
        cmd.Parameters.AddWithValue("phone", (object?)phone ?? DBNull.Value);
        cmd.Parameters.AddWithValue("createdBy", createdBy);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<bool> UpdateCollaboratorAsync(
        Guid tenantId,
        Guid userId,
        string? fullName,
        string? phone,
        string? accountState,
        Guid updatedBy,
        CancellationToken ct = default)
    {
        var conn = await GetOpenConnectionAsync(ct);
        var updatedUser = false;

        if (accountState is not null)
        {
            await using var userCmd = new NpgsqlCommand(
                """
                UPDATE identity.users
                SET account_state = @state, updated_at = now(), updated_by = @updatedBy
                WHERE id = @userId AND tenant_id = @tenantId AND deleted_at IS NULL
                """,
                conn);
            userCmd.Parameters.AddWithValue("state", accountState);
            userCmd.Parameters.AddWithValue("updatedBy", updatedBy);
            userCmd.Parameters.AddWithValue("userId", userId);
            userCmd.Parameters.AddWithValue("tenantId", tenantId);
            updatedUser = await userCmd.ExecuteNonQueryAsync(ct) > 0;
        }

        if (fullName is not null || phone is not null)
        {
            await using var profCmd = new NpgsqlCommand(
                """
                UPDATE identity.profiles
                SET
                  full_name = COALESCE(@fullName::text, full_name),
                  phone = COALESCE(@phone::text, phone),
                  updated_at = now(),
                  updated_by = @updatedBy
                WHERE user_id = @userId AND tenant_id = @tenantId AND deleted_at IS NULL
                """,
                conn);
            profCmd.Parameters.AddWithValue("fullName", (object?)fullName ?? DBNull.Value);
            profCmd.Parameters.AddWithValue("phone", (object?)phone ?? DBNull.Value);
            profCmd.Parameters.AddWithValue("updatedBy", updatedBy);
            profCmd.Parameters.AddWithValue("userId", userId);
            profCmd.Parameters.AddWithValue("tenantId", tenantId);
            var profileRows = await profCmd.ExecuteNonQueryAsync(ct);
            return profileRows > 0 || updatedUser;
        }

        return updatedUser;
    }

    public async Task<IReadOnlyList<AssignableRoleRow>> ListAssignableRolesAsync(
        bool excludeSuperAdmin,
        CancellationToken ct = default)
    {
        var conn = await GetOpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(
            """
            SELECT id, slug, name, scope
            FROM identity.roles
            WHERE tenant_id IS NULL AND deleted_at IS NULL AND scope = 'global'
              AND (@excludeSuperAdmin = false OR slug <> 'super-admin')
            ORDER BY name
            """,
            conn);
        cmd.Parameters.AddWithValue("excludeSuperAdmin", excludeSuperAdmin);

        var rows = new List<AssignableRoleRow>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            rows.Add(new AssignableRoleRow(
                reader.GetGuid(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetString(3)));
        }

        return rows;
    }

    public async Task<AssignableRoleRow?> GetRoleByIdAsync(Guid roleId, CancellationToken ct = default)
    {
        var conn = await GetOpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(
            """
            SELECT id, slug, name, scope
            FROM identity.roles
            WHERE id = @id AND deleted_at IS NULL
            """,
            conn);
        cmd.Parameters.AddWithValue("id", roleId);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
            return null;

        return new AssignableRoleRow(
            reader.GetGuid(0),
            reader.GetString(1),
            reader.GetString(2),
            reader.GetString(3));
    }

    public async Task EnsureUserRoleAsync(
        Guid tenantId,
        Guid userId,
        Guid roleId,
        Guid actorId,
        CancellationToken ct = default)
    {
        var conn = await GetOpenConnectionAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);

        await using (var cmd = new NpgsqlCommand(
            """
            INSERT INTO identity.user_roles (tenant_id, user_id, role_id, created_by, updated_by)
            VALUES (@tenantId, @userId, @roleId, @actorId, @actorId)
            ON CONFLICT ON CONSTRAINT uq_user_roles_tenant_user_role DO UPDATE
              SET deleted_at = NULL, deleted_by = NULL, updated_at = now(), updated_by = @actorId
              WHERE identity.user_roles.deleted_at IS NOT NULL
            """,
            conn, tx))
        {
            cmd.Parameters.AddWithValue("tenantId", tenantId);
            cmd.Parameters.AddWithValue("userId", userId);
            cmd.Parameters.AddWithValue("roleId", roleId);
            cmd.Parameters.AddWithValue("actorId", actorId);
            await cmd.ExecuteNonQueryAsync(ct);
        }

        await BumpPermissionsEpochAsync(conn, tx, userId, ct);
        await tx.CommitAsync(ct);
    }

    public async Task<bool> RemoveUserRoleAsync(
        Guid tenantId,
        Guid userId,
        Guid roleId,
        Guid actorId,
        CancellationToken ct = default)
    {
        var conn = await GetOpenConnectionAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);

        await using var cmd = new NpgsqlCommand(
            """
            UPDATE identity.user_roles
            SET deleted_at = now(), deleted_by = @actorId, updated_at = now(), updated_by = @actorId
            WHERE tenant_id = @tenantId AND user_id = @userId AND role_id = @roleId
              AND deleted_at IS NULL
            """,
            conn, tx);
        cmd.Parameters.AddWithValue("tenantId", tenantId);
        cmd.Parameters.AddWithValue("userId", userId);
        cmd.Parameters.AddWithValue("roleId", roleId);
        cmd.Parameters.AddWithValue("actorId", actorId);
        var removed = await cmd.ExecuteNonQueryAsync(ct) > 0;

        if (removed)
            await BumpPermissionsEpochAsync(conn, tx, userId, ct);

        await tx.CommitAsync(ct);
        return removed;
    }

    private static async Task BumpPermissionsEpochAsync(
        NpgsqlConnection conn,
        NpgsqlTransaction tx,
        Guid userId,
        CancellationToken ct)
    {
        await using var cmd = new NpgsqlCommand(
            """
            UPDATE identity.users
            SET permissions_epoch = permissions_epoch + 1,
                updated_at = now()
            WHERE id = @userId AND deleted_at IS NULL
            """,
            conn, tx);
        cmd.Parameters.AddWithValue("userId", userId);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    private static TenantRow MapTenant(NpgsqlDataReader reader) =>
        new(
            reader.GetGuid(0),
            reader.GetString(1),
            reader.GetString(2),
            reader.GetString(3),
            reader.GetString(4),
            reader.GetString(5),
            reader.GetInt32(6));

    private async Task<NpgsqlConnection> GetOpenConnectionAsync(CancellationToken ct)
    {
        var conn = (NpgsqlConnection)db.Database.GetDbConnection();
        if (conn.State != System.Data.ConnectionState.Open)
            await conn.OpenAsync(ct);
        return conn;
    }
}
