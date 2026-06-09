using Flit.Infrastructure.MultiTenant;
using Flit.Infrastructure.Persistence;
using Flit.Modules.Identity.Domain;
using Flit.Modules.Identity.Ports;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Flit.Infrastructure.Repositories;

public sealed class NpgsqlIdentityOnboardingRepository(FlitDbContext db) : IIdentityOnboardingRepository
{
    public async Task<PasswordComplexityPolicyRow> GetPasswordComplexityPolicyAsync(
        Guid tenantId, CancellationToken ct = default)
    {
        var conn = await GetOpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(
            """
            SELECT min_length, require_uppercase, require_lowercase, require_digit, require_symbol,
                   history_count, max_age_days, lockout_threshold, lockout_minutes
            FROM identity.password_policies
            WHERE tenant_id = @tenantId AND deleted_at IS NULL
            LIMIT 1
            """,
            conn);
        cmd.Parameters.AddWithValue("tenantId", tenantId);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
            return PasswordComplexityPolicyRow.Default;

        return new PasswordComplexityPolicyRow(
            reader.GetInt32(0),
            reader.GetBoolean(1),
            reader.GetBoolean(2),
            reader.GetBoolean(3),
            reader.GetBoolean(4),
            reader.GetInt32(5),
            reader.GetInt32(6),
            reader.GetInt32(7),
            reader.GetInt32(8));
    }

    public async Task<OnboardingInvitationRow?> FindInvitationByTokenHashAsync(
        string tokenHash, CancellationToken ct = default)
    {
        var conn = await GetOpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(
            """
            SELECT id, tenant_id, email, invited_role_id, token_hash, signature, status, expires_at, consumed_at
            FROM identity.find_onboarding_by_token_hash(@hash)
            """,
            conn);
        cmd.Parameters.AddWithValue("hash", tokenHash);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
            return null;

        return MapInvitation(reader);
    }

    public async Task<bool> HasPendingInvitationAsync(
        Guid tenantId, string email, CancellationToken ct = default)
    {
        var conn = await GetOpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(
            """
            SELECT EXISTS (
              SELECT 1 FROM identity.onboarding_invitations
              WHERE tenant_id = @tenantId AND email = @email::citext
                AND status = 'pending' AND deleted_at IS NULL
            )
            """,
            conn);
        cmd.Parameters.AddWithValue("tenantId", tenantId);
        cmd.Parameters.AddWithValue("email", email);
        return (bool)(await cmd.ExecuteScalarAsync(ct) ?? false);
    }

    public async Task<bool> HasActiveUserWithEmailAsync(string email, CancellationToken ct = default)
    {
        var conn = await GetOpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(
            "SELECT identity.email_has_active_user(@email::citext)",
            conn);
        cmd.Parameters.AddWithValue("email", email);
        return (bool)(await cmd.ExecuteScalarAsync(ct) ?? false);
    }

    public async Task<Guid?> FindInactiveUserIdByEmailAsync(
        Guid tenantId, string email, CancellationToken ct = default)
    {
        var conn = await GetOpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(
            """
            SELECT id FROM identity.users
            WHERE tenant_id = @tenantId AND email = @email::citext
              AND account_state = 'inactive' AND deleted_at IS NULL
            LIMIT 1
            """,
            conn);
        cmd.Parameters.AddWithValue("tenantId", tenantId);
        cmd.Parameters.AddWithValue("email", email);
        var result = await cmd.ExecuteScalarAsync(ct);
        return result is Guid g ? g : null;
    }

    public async Task<Guid> CreateInactiveUserAsync(
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

    public async Task<OnboardingInvitationRow> CreateInvitationAsync(
        Guid invitationId,
        Guid tenantId,
        string email,
        Guid invitedRoleId,
        string tokenHash,
        string signature,
        DateTimeOffset expiresAt,
        Guid createdBy,
        CancellationToken ct = default)
    {
        var id = invitationId;
        var conn = await GetOpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(
            """
            INSERT INTO identity.onboarding_invitations (
              id, tenant_id, email, invited_role_id, token_hash, signature,
              status, expires_at, created_by, updated_by
            ) VALUES (
              @id, @tenantId, @email::citext, @roleId, @tokenHash, @signature,
              'pending', @expiresAt, @createdBy, @createdBy
            )
            RETURNING id, tenant_id, email::text, invited_role_id, token_hash, signature,
                      status, expires_at, consumed_at
            """,
            conn);
        cmd.Parameters.AddWithValue("id", id);
        cmd.Parameters.AddWithValue("tenantId", tenantId);
        cmd.Parameters.AddWithValue("email", email);
        cmd.Parameters.AddWithValue("roleId", invitedRoleId);
        cmd.Parameters.AddWithValue("tokenHash", tokenHash);
        cmd.Parameters.AddWithValue("signature", signature);
        cmd.Parameters.AddWithValue("expiresAt", expiresAt);
        cmd.Parameters.AddWithValue("createdBy", createdBy);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        await reader.ReadAsync(ct);
        return MapInvitation(reader);
    }

    public async Task RevokePendingInvitationsAsync(
        Guid tenantId, string email, Guid updatedBy, CancellationToken ct = default)
    {
        var conn = await GetOpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(
            """
            UPDATE identity.onboarding_invitations
            SET status = 'revoked', updated_at = now(), updated_by = @updatedBy
            WHERE tenant_id = @tenantId AND email = @email::citext
              AND status = 'pending' AND deleted_at IS NULL
            """,
            conn);
        cmd.Parameters.AddWithValue("tenantId", tenantId);
        cmd.Parameters.AddWithValue("email", email);
        cmd.Parameters.AddWithValue("updatedBy", updatedBy);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task MarkInvitationExpiredAsync(Guid invitationId, Guid updatedBy, CancellationToken ct = default)
    {
        var conn = await GetOpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(
            """
            UPDATE identity.onboarding_invitations
            SET status = 'expired', updated_at = now(), updated_by = @updatedBy
            WHERE id = @id AND deleted_at IS NULL
            """,
            conn);
        cmd.Parameters.AddWithValue("id", invitationId);
        cmd.Parameters.AddWithValue("updatedBy", updatedBy);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task ConsumeInvitationAsync(
        Guid invitationId,
        DateTimeOffset consumedAt,
        Guid updatedBy,
        CancellationToken ct = default)
    {
        var conn = await GetOpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(
            """
            UPDATE identity.onboarding_invitations
            SET status = 'consumed', consumed_at = @consumedAt,
                updated_at = @consumedAt, updated_by = @updatedBy
            WHERE id = @id AND deleted_at IS NULL
            """,
            conn);
        cmd.Parameters.AddWithValue("id", invitationId);
        cmd.Parameters.AddWithValue("consumedAt", consumedAt);
        cmd.Parameters.AddWithValue("updatedBy", updatedBy);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task ActivateUserPasswordAsync(
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
                account_state = 'active',
                failed_attempt_count = 0,
                blocked_until = NULL,
                block_reason = NULL,
                updated_at = @at
            WHERE id = @id AND deleted_at IS NULL
            """,
            conn);
        cmd.Parameters.AddWithValue("id", userId);
        cmd.Parameters.AddWithValue("hash", passwordHash);
        cmd.Parameters.AddWithValue("at", at);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task EnsureUserRoleAsync(
        Guid tenantId,
        Guid userId,
        Guid roleId,
        Guid createdBy,
        CancellationToken ct = default)
    {
        var conn = await GetOpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(
            """
            INSERT INTO identity.user_roles (tenant_id, user_id, role_id, created_by, updated_by)
            VALUES (@tenantId, @userId, @roleId, @createdBy, @createdBy)
            ON CONFLICT (tenant_id, user_id, role_id) DO NOTHING
            """,
            conn);
        cmd.Parameters.AddWithValue("tenantId", tenantId);
        cmd.Parameters.AddWithValue("userId", userId);
        cmd.Parameters.AddWithValue("roleId", roleId);
        cmd.Parameters.AddWithValue("createdBy", createdBy);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task ApplyTenantGucAsync(Guid tenantId, CancellationToken ct = default)
    {
        var conn = await GetOpenConnectionAsync(ct);
        var context = new TenantGucScope(tenantId);
        await using var cmd = new NpgsqlCommand(TenantGucApplicator.BuildSetConfigBatch(context), conn);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<Guid?> FindUserIdByEmailInTenantAsync(
        Guid tenantId, string email, CancellationToken ct = default)
    {
        var conn = await GetOpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(
            """
            SELECT id FROM identity.users
            WHERE tenant_id = @tenantId AND email = @email::citext AND deleted_at IS NULL
            LIMIT 1
            """,
            conn);
        cmd.Parameters.AddWithValue("tenantId", tenantId);
        cmd.Parameters.AddWithValue("email", email);
        var result = await cmd.ExecuteScalarAsync(ct);
        return result is Guid g ? g : null;
    }

    public async Task<bool> RoleExistsAsync(Guid roleId, CancellationToken ct = default)
    {
        var conn = await GetOpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(
            """
            SELECT EXISTS (
              SELECT 1 FROM identity.roles WHERE id = @id AND deleted_at IS NULL
            )
            """,
            conn);
        cmd.Parameters.AddWithValue("id", roleId);
        return (bool)(await cmd.ExecuteScalarAsync(ct) ?? false);
    }

    private async Task<NpgsqlConnection> GetOpenConnectionAsync(CancellationToken ct)
    {
        var conn = (NpgsqlConnection)db.Database.GetDbConnection();
        if (conn.State != System.Data.ConnectionState.Open)
            await conn.OpenAsync(ct);
        return conn;
    }

    private static OnboardingInvitationRow MapInvitation(NpgsqlDataReader reader) =>
        new(
            reader.GetGuid(0),
            reader.GetGuid(1),
            reader.GetString(2),
            reader.GetGuid(3),
            reader.GetString(4),
            reader.GetString(5),
            reader.GetString(6),
            reader.GetFieldValue<DateTimeOffset>(7),
            reader.IsDBNull(8) ? null : reader.GetFieldValue<DateTimeOffset>(8));

    private sealed class TenantGucScope(Guid tenantId) : ITenantContext
    {
        public Guid? TenantId => tenantId;
        public Guid? UserId => Guid.Parse("00000000-0000-7000-8000-000000000001");
        public bool IsSuperAdmin => false;
        public Guid? TrafficAgencyId => null;
        public Guid? RequestId => null;
        public string? ClientIp => null;
        public bool IsConfigured => true;

        public void Apply(
            Guid? tenantId,
            Guid? userId,
            bool isSuperAdmin,
            Guid? trafficAgencyId = null,
            Guid? requestId = null,
            string? clientIp = null)
        { }
    }
}
