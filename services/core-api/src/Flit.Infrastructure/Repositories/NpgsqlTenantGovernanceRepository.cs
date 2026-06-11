using Flit.Infrastructure.Persistence;
using Flit.Modules.Companies.Ports;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Flit.Infrastructure.Repositories;

public sealed class NpgsqlTenantGovernanceRepository(FlitDbContext db) : ITenantGovernanceRepository
{
    public async Task<IReadOnlyList<TenantUserExceptionRow>> ListUserExceptionsAsync(
        Guid tenantId,
        CancellationToken ct = default)
    {
        var conn = await OpenWithTenantSessionAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT id, tenant_id, user_id, reason, expires_at, created_at
            FROM companies.tenant_user_exceptions
            WHERE tenant_id = @tenant_id
              AND deleted_at IS NULL
            ORDER BY created_at DESC
            """;
        cmd.Parameters.AddWithValue("tenant_id", tenantId);

        var items = new List<TenantUserExceptionRow>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            items.Add(MapException(reader));
        }

        return items;
    }

    public async Task<TenantUserExceptionRow?> GetUserExceptionByIdAsync(
        Guid tenantId,
        Guid exceptionId,
        CancellationToken ct = default)
    {
        var conn = await OpenWithTenantSessionAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT id, tenant_id, user_id, reason, expires_at, created_at
            FROM companies.tenant_user_exceptions
            WHERE tenant_id = @tenant_id
              AND id = @id
              AND deleted_at IS NULL
            """;
        cmd.Parameters.AddWithValue("tenant_id", tenantId);
        cmd.Parameters.AddWithValue("id", exceptionId);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        return await reader.ReadAsync(ct) ? MapException(reader) : null;
    }

    public async Task<Guid> CreateUserExceptionAsync(
        Guid tenantId,
        Guid userId,
        string? reason,
        DateTimeOffset? expiresAt,
        Guid actorUserId,
        CancellationToken ct = default)
    {
        var id = Guid.CreateVersion7();
        var conn = await OpenWithTenantSessionAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO companies.tenant_user_exceptions (
              id, tenant_id, user_id, reason, expires_at, created_by, updated_by
            )
            VALUES (
              @id, @tenant_id, @user_id, @reason, @expires_at, @actor, @actor
            )
            RETURNING id
            """;
        cmd.Parameters.AddWithValue("id", id);
        cmd.Parameters.AddWithValue("tenant_id", tenantId);
        cmd.Parameters.AddWithValue("user_id", userId);
        cmd.Parameters.AddWithValue("reason", (object?)reason ?? DBNull.Value);
        cmd.Parameters.AddWithValue("expires_at", (object?)expiresAt ?? DBNull.Value);
        cmd.Parameters.AddWithValue("actor", actorUserId);

        var result = await cmd.ExecuteScalarAsync(ct);
        return result is Guid returned ? returned : id;
    }

    public async Task<bool> SoftDeleteUserExceptionAsync(
        Guid tenantId,
        Guid exceptionId,
        Guid actorUserId,
        CancellationToken ct = default)
    {
        var conn = await OpenWithTenantSessionAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            UPDATE companies.tenant_user_exceptions
            SET deleted_at = now(),
                deleted_by = @actor,
                updated_at = now(),
                updated_by = @actor
            WHERE tenant_id = @tenant_id
              AND id = @id
              AND deleted_at IS NULL
            """;
        cmd.Parameters.AddWithValue("tenant_id", tenantId);
        cmd.Parameters.AddWithValue("id", exceptionId);
        cmd.Parameters.AddWithValue("actor", actorUserId);

        return await cmd.ExecuteNonQueryAsync(ct) > 0;
    }

    public async Task<bool> UserBelongsToTenantAsync(
        Guid tenantId,
        Guid userId,
        CancellationToken ct = default)
    {
        var conn = await OpenWithTenantSessionAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT EXISTS(
              SELECT 1
              FROM identity.users
              WHERE id = @user_id
                AND tenant_id = @tenant_id
                AND deleted_at IS NULL
            )
            """;
        cmd.Parameters.AddWithValue("tenant_id", tenantId);
        cmd.Parameters.AddWithValue("user_id", userId);

        return Convert.ToBoolean(
            await cmd.ExecuteScalarAsync(ct) ?? false,
            System.Globalization.CultureInfo.InvariantCulture);
    }

    public async Task<bool> IsUserExemptFromOnlyOwnAsync(
        Guid tenantId,
        Guid userId,
        CancellationToken ct = default)
    {
        var conn = await OpenWithTenantSessionAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT EXISTS(
              SELECT 1
              FROM companies.tenant_user_exceptions
              WHERE tenant_id = @tenant_id
                AND user_id = @user_id
                AND deleted_at IS NULL
                AND (expires_at IS NULL OR expires_at > now())
            )
            """;
        cmd.Parameters.AddWithValue("tenant_id", tenantId);
        cmd.Parameters.AddWithValue("user_id", userId);

        return Convert.ToBoolean(
            await cmd.ExecuteScalarAsync(ct) ?? false,
            System.Globalization.CultureInfo.InvariantCulture);
    }

    public async Task<IReadOnlyList<TenantAuthorizedTrafficAgencyRow>> ListAuthorizedTrafficAgenciesAsync(
        Guid tenantId,
        CancellationToken ct = default)
    {
        var conn = await OpenWithTenantSessionAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT id, tenant_id, traffic_agency_id, is_enabled, updated_at
            FROM companies.tenant_authorized_traffic_agencies
            WHERE tenant_id = @tenant_id
              AND deleted_at IS NULL
            ORDER BY traffic_agency_id
            """;
        cmd.Parameters.AddWithValue("tenant_id", tenantId);

        var items = new List<TenantAuthorizedTrafficAgencyRow>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            items.Add(MapTrafficAgency(reader));
        }

        return items;
    }

    public async Task UpsertAuthorizedTrafficAgencyAsync(
        Guid tenantId,
        Guid trafficAgencyId,
        bool isEnabled,
        Guid actorUserId,
        CancellationToken ct = default)
    {
        var conn = await OpenWithTenantSessionAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO companies.tenant_authorized_traffic_agencies (
              tenant_id, traffic_agency_id, is_enabled, created_by, updated_by
            )
            VALUES (@tenant_id, @traffic_agency_id, @is_enabled, @actor, @actor)
            ON CONFLICT (tenant_id, traffic_agency_id) DO UPDATE SET
              is_enabled = EXCLUDED.is_enabled,
              updated_by = EXCLUDED.updated_by,
              updated_at = now(),
              deleted_at = NULL,
              deleted_by = NULL
            """;
        cmd.Parameters.AddWithValue("tenant_id", tenantId);
        cmd.Parameters.AddWithValue("traffic_agency_id", trafficAgencyId);
        cmd.Parameters.AddWithValue("is_enabled", isEnabled);
        cmd.Parameters.AddWithValue("actor", actorUserId);

        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<bool?> GetTrafficAgencyEnabledAsync(
        Guid tenantId,
        Guid trafficAgencyId,
        CancellationToken ct = default)
    {
        var conn = await OpenWithTenantSessionAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT is_enabled
            FROM companies.tenant_authorized_traffic_agencies
            WHERE tenant_id = @tenant_id
              AND traffic_agency_id = @traffic_agency_id
              AND deleted_at IS NULL
            """;
        cmd.Parameters.AddWithValue("tenant_id", tenantId);
        cmd.Parameters.AddWithValue("traffic_agency_id", trafficAgencyId);

        var scalar = await cmd.ExecuteScalarAsync(ct);
        return scalar is null or DBNull ? null : Convert.ToBoolean(scalar, System.Globalization.CultureInfo.InvariantCulture);
    }

    private async Task<NpgsqlConnection> OpenWithTenantSessionAsync(CancellationToken ct)
    {
        await db.Database.OpenConnectionAsync(ct);
        var conn = (NpgsqlConnection)db.Database.GetDbConnection();
        await TenantSessionGuc.ApplyAsync(conn, CompaniesSessionAmbient.Get(), ct);
        return conn;
    }

    private static TenantUserExceptionRow MapException(NpgsqlDataReader reader) => new(
        reader.GetGuid(0),
        reader.GetGuid(1),
        reader.GetGuid(2),
        reader.IsDBNull(3) ? null : reader.GetString(3),
        reader.IsDBNull(4) ? null : reader.GetFieldValue<DateTimeOffset>(4),
        reader.GetFieldValue<DateTimeOffset>(5));

    private static TenantAuthorizedTrafficAgencyRow MapTrafficAgency(NpgsqlDataReader reader) => new(
        reader.GetGuid(0),
        reader.GetGuid(1),
        reader.GetGuid(2),
        reader.GetBoolean(3),
        reader.GetFieldValue<DateTimeOffset>(4));
}
