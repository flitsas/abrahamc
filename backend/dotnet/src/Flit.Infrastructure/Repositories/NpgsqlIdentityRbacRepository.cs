using Flit.Infrastructure.Persistence;
using Flit.Modules.Identity.Domain;
using Flit.Modules.Identity.Ports;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Flit.Infrastructure.Repositories;

public sealed class NpgsqlIdentityRbacRepository(FlitDbContext db) : IIdentityRbacRepository
{
    public async Task<bool> UserHasPermissionSlugAsync(
        Guid userId, Guid tenantId, string slug, CancellationToken ct = default)
    {
        var conn = await GetOpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(
            """
            SELECT EXISTS (
              SELECT 1
              FROM identity.user_roles ur
              JOIN identity.role_permissions rp
                ON rp.role_id = ur.role_id AND rp.deleted_at IS NULL
              JOIN identity.permissions p
                ON p.id = rp.permission_id AND p.is_active = true AND p.slug = @slug
              WHERE ur.user_id = @userId
                AND ur.tenant_id = @tenantId
                AND ur.deleted_at IS NULL
                AND (rp.tenant_id IS NULL OR rp.tenant_id = ur.tenant_id)
            )
            """,
            conn);
        cmd.Parameters.AddWithValue("userId", userId);
        cmd.Parameters.AddWithValue("tenantId", tenantId);
        cmd.Parameters.AddWithValue("slug", slug);
        return (bool)(await cmd.ExecuteScalarAsync(ct) ?? false);
    }

    public async Task<IReadOnlyList<string>> GetEffectiveSlugsAsync(
        Guid userId, Guid tenantId, CancellationToken ct = default)
    {
        var conn = await GetOpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(
            """
            SELECT DISTINCT p.slug
            FROM identity.user_roles ur
            JOIN identity.role_permissions rp
              ON rp.role_id = ur.role_id AND rp.deleted_at IS NULL
            JOIN identity.permissions p
              ON p.id = rp.permission_id AND p.is_active = true
            WHERE ur.user_id = @userId
              AND ur.tenant_id = @tenantId
              AND ur.deleted_at IS NULL
              AND (rp.tenant_id IS NULL OR rp.tenant_id = ur.tenant_id)
            ORDER BY p.slug
            """,
            conn);
        cmd.Parameters.AddWithValue("userId", userId);
        cmd.Parameters.AddWithValue("tenantId", tenantId);

        var slugs = new List<string>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            slugs.Add(reader.GetString(0));

        return slugs;
    }

    public async Task<IReadOnlyList<PermissionSlugRow>> ListPermissionsAsync(CancellationToken ct = default)
    {
        var conn = await GetOpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(
            """
            SELECT id, slug, module, action, description, is_active, is_assignable, is_system
            FROM identity.permissions
            ORDER BY slug
            """,
            conn);

        var rows = new List<PermissionSlugRow>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            rows.Add(MapPermission(reader));

        return rows;
    }

    public async Task<PermissionSlugRow?> GetPermissionByIdAsync(Guid id, CancellationToken ct = default)
    {
        var conn = await GetOpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(
            """
            SELECT id, slug, module, action, description, is_active, is_assignable, is_system
            FROM identity.permissions
            WHERE id = @id
            """,
            conn);
        cmd.Parameters.AddWithValue("id", id);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        return await reader.ReadAsync(ct) ? MapPermission(reader) : null;
    }

    public async Task<PermissionSlugRow?> GetPermissionBySlugAsync(string slug, CancellationToken ct = default)
    {
        var conn = await GetOpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(
            """
            SELECT id, slug, module, action, description, is_active, is_assignable, is_system
            FROM identity.permissions
            WHERE slug = @slug
            """,
            conn);
        cmd.Parameters.AddWithValue("slug", slug);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        return await reader.ReadAsync(ct) ? MapPermission(reader) : null;
    }

    public async Task<PermissionSlugRow> CreatePermissionAsync(
        string slug,
        string moduleName,
        string action,
        string? description,
        Guid? createdBy,
        CancellationToken ct = default)
    {
        var conn = await GetOpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(
            """
            INSERT INTO identity.permissions (slug, module, action, description, is_assignable, is_system, is_active)
            VALUES (@slug, @module, @action, @description, true, false, true)
            RETURNING id, slug, module, action, description, is_active, is_assignable, is_system
            """,
            conn);
        cmd.Parameters.AddWithValue("slug", slug);
        cmd.Parameters.AddWithValue("module", moduleName);
        cmd.Parameters.AddWithValue("action", action);
        cmd.Parameters.AddWithValue("description", (object?)description ?? DBNull.Value);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
            throw new InvalidOperationException("No se pudo crear el permiso.");

        return MapPermission(reader);
    }

    public async Task<bool> SetPermissionActiveAsync(
        Guid id, bool isActive, CancellationToken ct = default)
    {
        var rows = await db.Database.ExecuteSqlInterpolatedAsync(
            $"""
            UPDATE identity.permissions
            SET is_active = {isActive}, updated_at = now()
            WHERE id = {id}
            """,
            ct);
        return rows > 0;
    }

    private async Task<NpgsqlConnection> GetOpenConnectionAsync(CancellationToken ct)
    {
        var conn = (NpgsqlConnection)db.Database.GetDbConnection();
        if (conn.State != System.Data.ConnectionState.Open)
            await conn.OpenAsync(ct);
        return conn;
    }

    private static PermissionSlugRow MapPermission(NpgsqlDataReader reader) => new(
        reader.GetGuid(0),
        reader.GetString(1),
        reader.GetString(2),
        reader.GetString(3),
        reader.IsDBNull(4) ? null : reader.GetString(4),
        reader.GetBoolean(5),
        reader.GetBoolean(6),
        reader.GetBoolean(7));
}
