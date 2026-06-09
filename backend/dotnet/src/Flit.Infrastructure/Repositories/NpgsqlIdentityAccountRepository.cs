using Flit.Infrastructure.Persistence;
using Flit.Modules.Identity.Domain;
using Flit.Modules.Identity.Ports;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Flit.Infrastructure.Repositories;

public sealed class NpgsqlIdentityAccountRepository(FlitDbContext db) : IIdentityAccountRepository
{
    public async Task<AuthUserRow?> FindForAuthAsync(string email, CancellationToken ct = default)
    {
        var conn = await GetOpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(
            """
            SELECT f.id, f.tenant_id, u.email::text, f.password_hash, f.password_algo, f.account_state, f.blocked_until
            FROM identity.find_user_for_auth(@email::citext) f
            JOIN identity.users u ON u.id = f.id
            """,
            conn);
        cmd.Parameters.AddWithValue("email", email);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
            return null;

        return MapUser(reader);
    }

    public async Task<AuthUserRow?> FindByIdAsync(Guid userId, CancellationToken ct = default)
    {
        var conn = await GetOpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(
            """
            SELECT id, tenant_id, email::text, password_hash, password_algo, account_state, blocked_until
            FROM identity.users
            WHERE id = @id AND deleted_at IS NULL
            """,
            conn);
        cmd.Parameters.AddWithValue("id", userId);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
            return null;

        return MapUser(reader);
    }

    public async Task<IReadOnlyList<string>> GetPermissionSlugsAsync(
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

    public async Task<bool> IsSuperAdminAsync(Guid userId, CancellationToken ct = default)
    {
        var conn = await GetOpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(
            """
            SELECT EXISTS (
              SELECT 1
              FROM identity.user_roles ur
              JOIN identity.roles r ON r.id = ur.role_id AND r.deleted_at IS NULL
              WHERE ur.user_id = @userId
                AND ur.deleted_at IS NULL
                AND r.slug = 'super-admin'
                AND r.scope = 'global'
            )
            """,
            conn);
        cmd.Parameters.AddWithValue("userId", userId);
        return (bool)(await cmd.ExecuteScalarAsync(ct) ?? false);
    }

    public async Task RecordLoginAttemptAsync(
        string email,
        bool succeeded,
        string? failureReason,
        Guid? tenantId,
        Guid? userId,
        string? ipAddress,
        string? userAgent,
        CancellationToken ct = default)
    {
        var conn = await GetOpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(
            """
            INSERT INTO identity.login_attempts (
              tenant_id, user_id, email_attempted, succeeded, failure_reason, ip_address, user_agent
            ) VALUES (
              @tenantId, @userId, @email, @succeeded, @failureReason,
              CASE WHEN @ip IS NULL OR @ip = '' THEN NULL ELSE @ip::inet END,
              @userAgent
            )
            """,
            conn);
        cmd.Parameters.AddWithValue("tenantId", (object?)tenantId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("userId", (object?)userId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("email", email);
        cmd.Parameters.AddWithValue("succeeded", succeeded);
        cmd.Parameters.AddWithValue("failureReason", (object?)failureReason ?? DBNull.Value);
        cmd.Parameters.AddWithValue("ip", (object?)ipAddress ?? DBNull.Value);
        cmd.Parameters.AddWithValue("userAgent", (object?)userAgent ?? DBNull.Value);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task TouchLastLoginAsync(Guid userId, DateTimeOffset at, CancellationToken ct = default)
    {
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"""
            UPDATE identity.users
            SET last_login_at = {at}, updated_at = {at}
            WHERE id = {userId} AND deleted_at IS NULL
            """,
            ct);
    }

    public async Task StoreRefreshTokenAsync(
        Guid tenantId,
        Guid userId,
        string tokenHash,
        DateTimeOffset expiresAt,
        string? userAgent,
        string? ipAddress,
        CancellationToken ct = default)
    {
        var conn = await GetOpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(
            """
            INSERT INTO identity.refresh_tokens (
              tenant_id, user_id, token_hash, expires_at, user_agent, ip_address,
              created_by, updated_by
            ) VALUES (
              @tenantId, @userId, @tokenHash, @expiresAt, @userAgent,
              CASE WHEN @ip IS NULL OR @ip = '' THEN NULL ELSE @ip::inet END,
              @userId, @userId
            )
            """,
            conn);
        cmd.Parameters.AddWithValue("tenantId", tenantId);
        cmd.Parameters.AddWithValue("userId", userId);
        cmd.Parameters.AddWithValue("tokenHash", tokenHash);
        cmd.Parameters.AddWithValue("expiresAt", expiresAt);
        cmd.Parameters.AddWithValue("userAgent", (object?)userAgent ?? DBNull.Value);
        cmd.Parameters.AddWithValue("ip", (object?)ipAddress ?? DBNull.Value);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<RefreshTokenRow?> FindRefreshTokenByHashAsync(
        string tokenHash, CancellationToken ct = default)
    {
        var conn = await GetOpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(
            """
            SELECT id, tenant_id, user_id, expires_at, revoked_at
            FROM identity.refresh_tokens
            WHERE token_hash = @hash AND deleted_at IS NULL
            """,
            conn);
        cmd.Parameters.AddWithValue("hash", tokenHash);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
            return null;

        return new RefreshTokenRow(
            reader.GetGuid(0),
            reader.GetGuid(1),
            reader.GetGuid(2),
            reader.GetFieldValue<DateTimeOffset>(3),
            reader.IsDBNull(4) ? null : reader.GetFieldValue<DateTimeOffset>(4));
    }

    public async Task RevokeRefreshTokenAsync(
        string tokenHash, DateTimeOffset revokedAt, CancellationToken ct = default)
    {
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"""
            UPDATE identity.refresh_tokens
            SET revoked_at = {revokedAt}, updated_at = {revokedAt}
            WHERE token_hash = {tokenHash} AND deleted_at IS NULL
            """,
            ct);
    }

    public async Task<PasswordPolicyRow> GetPasswordPolicyAsync(Guid tenantId, CancellationToken ct = default)
    {
        var conn = await GetOpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(
            """
            SELECT lockout_threshold, lockout_minutes
            FROM identity.password_policies
            WHERE tenant_id = @tenantId AND deleted_at IS NULL
            ORDER BY created_at
            LIMIT 1
            """,
            conn);
        cmd.Parameters.AddWithValue("tenantId", tenantId);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
            return new PasswordPolicyRow(5, 15);

        return new PasswordPolicyRow(reader.GetInt32(0), reader.GetInt32(1));
    }

    public async Task<int> IncrementFailedAttemptsAsync(Guid userId, CancellationToken ct = default)
    {
        var conn = await GetOpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(
            """
            UPDATE identity.users
            SET failed_attempt_count = failed_attempt_count + 1,
                updated_at = now()
            WHERE id = @id AND deleted_at IS NULL
            RETURNING failed_attempt_count
            """,
            conn);
        cmd.Parameters.AddWithValue("id", userId);
        return (int)(await cmd.ExecuteScalarAsync(ct) ?? 0);
    }

    public async Task ResetFailedAttemptsAsync(Guid userId, CancellationToken ct = default)
    {
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"""
            UPDATE identity.users
            SET failed_attempt_count = 0, updated_at = {DateTimeOffset.UtcNow}
            WHERE id = {userId} AND deleted_at IS NULL
            """,
            ct);
    }

    public async Task ApplyTempBlockAsync(
        Guid userId,
        DateTimeOffset blockedUntil,
        string blockReason,
        DateTimeOffset at,
        CancellationToken ct = default)
    {
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"""
            UPDATE identity.users
            SET account_state = 'temp_blocked',
                blocked_until = {blockedUntil},
                block_reason = {blockReason},
                failed_attempt_count = 0,
                updated_at = {at}
            WHERE id = {userId} AND deleted_at IS NULL
            """,
            ct);
    }

    public async Task RehabilitateAccountAsync(Guid userId, DateTimeOffset at, CancellationToken ct = default)
    {
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"""
            UPDATE identity.users
            SET account_state = 'active',
                blocked_until = NULL,
                block_reason = NULL,
                failed_attempt_count = 0,
                updated_at = {at}
            WHERE id = {userId} AND deleted_at IS NULL
            """,
            ct);
    }

    /// <summary>Conexión del DbContext; no dispone — EF la gestiona.</summary>
    private async Task<NpgsqlConnection> GetOpenConnectionAsync(CancellationToken ct)
    {
        var conn = (NpgsqlConnection)db.Database.GetDbConnection();
        if (conn.State != System.Data.ConnectionState.Open)
            await conn.OpenAsync(ct);
        return conn;
    }

    private static AuthUserRow MapUser(NpgsqlDataReader reader) => new(
        reader.GetGuid(0),
        reader.GetGuid(1),
        reader.GetString(2),
        reader.IsDBNull(3) ? null : reader.GetString(3),
        reader.GetString(4),
        reader.GetString(5),
        reader.IsDBNull(6) ? null : reader.GetFieldValue<DateTimeOffset>(6));
}
