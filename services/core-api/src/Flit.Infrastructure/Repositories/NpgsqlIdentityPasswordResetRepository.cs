using Flit.Infrastructure.Persistence;
using Flit.Modules.Identity.Ports;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Flit.Infrastructure.Repositories;

public sealed class NpgsqlIdentityPasswordResetRepository(FlitDbContext db) : IIdentityPasswordResetRepository
{
    private static readonly Guid SystemUserId = Guid.Parse("00000000-0000-7000-8000-000000000001");

    public async Task CreateTokenAsync(
        Guid tenantId,
        Guid userId,
        string tokenHash,
        DateTimeOffset expiresAt,
        CancellationToken ct = default)
    {
        var conn = await GetOpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(
            """
            INSERT INTO identity.password_reset_tokens (
              id, tenant_id, user_id, token_hash, expires_at, created_by, updated_by
            ) VALUES (
              @id, @tenantId, @userId, @tokenHash, @expiresAt, @actor, @actor
            )
            """,
            conn);
        cmd.Parameters.AddWithValue("id", Guid.CreateVersion7());
        cmd.Parameters.AddWithValue("tenantId", tenantId);
        cmd.Parameters.AddWithValue("userId", userId);
        cmd.Parameters.AddWithValue("tokenHash", tokenHash);
        cmd.Parameters.AddWithValue("expiresAt", expiresAt);
        cmd.Parameters.AddWithValue("actor", SystemUserId);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<PasswordResetTokenRow?> FindValidByHashAsync(
        string tokenHash,
        DateTimeOffset now,
        CancellationToken ct = default)
    {
        var conn = await GetOpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(
            """
            SELECT id, tenant_id, user_id, expires_at
            FROM identity.password_reset_tokens
            WHERE token_hash = @hash
              AND consumed_at IS NULL
              AND deleted_at IS NULL
              AND expires_at > @now
            LIMIT 1
            """,
            conn);
        cmd.Parameters.AddWithValue("hash", tokenHash);
        cmd.Parameters.AddWithValue("now", now);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
            return null;

        return new PasswordResetTokenRow(
            reader.GetGuid(0),
            reader.GetGuid(1),
            reader.GetGuid(2),
            reader.GetFieldValue<DateTimeOffset>(3));
    }

    public async Task ConsumeAndUpdatePasswordAsync(
        Guid tokenId,
        Guid userId,
        string passwordHash,
        DateTimeOffset at,
        CancellationToken ct = default)
    {
        var conn = await GetOpenConnectionAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);

        await using (var updateUser = new NpgsqlCommand(
            """
            UPDATE identity.users
            SET password_hash = @hash,
                password_algo = 'argon2id',
                failed_attempt_count = 0,
                blocked_until = NULL,
                block_reason = NULL,
                updated_at = @at
            WHERE id = @userId AND deleted_at IS NULL
            """,
            conn, tx))
        {
            updateUser.Parameters.AddWithValue("hash", passwordHash);
            updateUser.Parameters.AddWithValue("at", at);
            updateUser.Parameters.AddWithValue("userId", userId);
            await updateUser.ExecuteNonQueryAsync(ct);
        }

        await using (var consume = new NpgsqlCommand(
            """
            UPDATE identity.password_reset_tokens
            SET consumed_at = @at, updated_at = @at, updated_by = @actor
            WHERE id = @id AND deleted_at IS NULL
            """,
            conn, tx))
        {
            consume.Parameters.AddWithValue("at", at);
            consume.Parameters.AddWithValue("actor", SystemUserId);
            consume.Parameters.AddWithValue("id", tokenId);
            await consume.ExecuteNonQueryAsync(ct);
        }

        await tx.CommitAsync(ct);
    }

    private async Task<NpgsqlConnection> GetOpenConnectionAsync(CancellationToken ct)
    {
        var conn = (NpgsqlConnection)db.Database.GetDbConnection();
        if (conn.State != System.Data.ConnectionState.Open)
            await conn.OpenAsync(ct);
        return conn;
    }
}
