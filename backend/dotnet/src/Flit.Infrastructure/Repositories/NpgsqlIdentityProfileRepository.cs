using Flit.Infrastructure.Persistence;
using Flit.Modules.Identity.Domain;
using Flit.Modules.Identity.Ports;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Flit.Infrastructure.Repositories;

public sealed class NpgsqlIdentityProfileRepository(FlitDbContext db) : IIdentityProfileRepository
{
    public Task ApplySessionGucAsync(
        Guid tenantId,
        Guid userId,
        bool isSuperAdmin,
        CancellationToken ct = default) =>
        IdentitySessionGucApplicator.ApplyAsync(db, tenantId, userId, isSuperAdmin, ct);

    public async Task<ProfileRow?> GetProfileAsync(
        Guid tenantId,
        Guid userId,
        CancellationToken ct = default)
    {
        var conn = await GetOpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(
            """
            SELECT u.id, u.tenant_id, u.email::text, u.account_state,
                   p.full_name, p.phone, p.address, p.locale, p.timezone, u.row_version,
                   COALESCE(
                     (SELECT array_agg(r.slug ORDER BY r.slug)
                      FROM identity.user_roles ur
                      JOIN identity.roles r ON r.id = ur.role_id AND r.deleted_at IS NULL
                      WHERE ur.tenant_id = u.tenant_id AND ur.user_id = u.id AND ur.deleted_at IS NULL),
                     ARRAY[]::text[]
                   ) AS role_slugs
            FROM identity.users u
            LEFT JOIN identity.profiles p ON p.user_id = u.id AND p.deleted_at IS NULL
            WHERE u.id = @userId AND u.tenant_id = @tenantId AND u.deleted_at IS NULL
            """,
            conn);
        cmd.Parameters.AddWithValue("userId", userId);
        cmd.Parameters.AddWithValue("tenantId", tenantId);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
            return null;

        return new ProfileRow(
            reader.GetGuid(0),
            reader.GetGuid(1),
            reader.GetString(2),
            reader.IsDBNull(4) ? null : reader.GetString(4),
            reader.IsDBNull(5) ? null : reader.GetString(5),
            reader.IsDBNull(6) ? null : reader.GetString(6),
            reader.IsDBNull(7) ? "es-CO" : reader.GetString(7),
            reader.IsDBNull(8) ? "America/Bogota" : reader.GetString(8),
            reader.GetString(3),
            reader.GetFieldValue<string[]>(10),
            reader.GetInt32(9));
    }

    public async Task<bool> UpdateProfileAsync(
        Guid tenantId,
        Guid userId,
        string? fullName,
        string? phone,
        string? locale,
        Guid updatedBy,
        CancellationToken ct = default)
    {
        var conn = await GetOpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(
            """
            UPDATE identity.profiles
            SET full_name = COALESCE(@fullName, full_name),
                phone = COALESCE(@phone, phone),
                locale = COALESCE(@locale, locale),
                updated_at = now(),
                updated_by = @updatedBy
            WHERE user_id = @userId
              AND tenant_id = @tenantId
              AND deleted_at IS NULL
            """,
            conn);
        cmd.Parameters.AddWithValue("userId", userId);
        cmd.Parameters.AddWithValue("tenantId", tenantId);
        cmd.Parameters.AddWithValue("fullName", (object?)fullName ?? DBNull.Value);
        cmd.Parameters.AddWithValue("phone", (object?)phone ?? DBNull.Value);
        cmd.Parameters.AddWithValue("locale", (object?)locale ?? DBNull.Value);
        cmd.Parameters.AddWithValue("updatedBy", updatedBy);

        return await cmd.ExecuteNonQueryAsync(ct) > 0;
    }

    public async Task<bool> UpdatePasswordAsync(
        Guid userId,
        string passwordHash,
        DateTimeOffset at,
        CancellationToken ct = default)
    {
        var conn = await GetOpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(
            """
            UPDATE identity.users
            SET password_hash = @hash,
                password_algo = 'argon2id',
                updated_at = @at,
                updated_by = @userId
            WHERE id = @userId AND deleted_at IS NULL
            """,
            conn);
        cmd.Parameters.AddWithValue("userId", userId);
        cmd.Parameters.AddWithValue("hash", passwordHash);
        cmd.Parameters.AddWithValue("at", at);

        return await cmd.ExecuteNonQueryAsync(ct) > 0;
    }

    private async Task<NpgsqlConnection> GetOpenConnectionAsync(CancellationToken ct)
    {
        var conn = (NpgsqlConnection)db.Database.GetDbConnection();
        if (conn.State != System.Data.ConnectionState.Open)
            await conn.OpenAsync(ct);
        return conn;
    }
}
