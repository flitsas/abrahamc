using Flit.Infrastructure.Persistence;
using Flit.Modules.Identity.Domain;
using Flit.Modules.Identity.Ports;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Flit.Infrastructure.Repositories;

public sealed class NpgsqlIdentitySupportRepository(FlitDbContext db) : IIdentitySupportRepository
{
    public Task ApplySessionGucAsync(
        Guid tenantId,
        Guid userId,
        bool isSuperAdmin,
        CancellationToken ct = default) =>
        IdentitySessionGucApplicator.ApplyAsync(db, tenantId, userId, isSuperAdmin, ct);

    public async Task<SupportTicketRow> CreateTicketAsync(
        Guid tenantId,
        Guid reporterUserId,
        string subject,
        string body,
        string category,
        Guid createdBy,
        CancellationToken ct = default)
    {
        var id = Guid.CreateVersion7();
        var conn = await GetOpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(
            """
            INSERT INTO identity.support_tickets (
              id, tenant_id, reporter_user_id, subject, body, category, status,
              created_by, updated_by)
            VALUES (
              @id, @tenantId, @reporterUserId, @subject, @body, @category, 'open',
              @createdBy, @createdBy)
            """,
            conn);
        cmd.Parameters.AddWithValue("id", id);
        cmd.Parameters.AddWithValue("tenantId", tenantId);
        cmd.Parameters.AddWithValue("reporterUserId", reporterUserId);
        cmd.Parameters.AddWithValue("subject", subject);
        cmd.Parameters.AddWithValue("body", body);
        cmd.Parameters.AddWithValue("category", category);
        cmd.Parameters.AddWithValue("createdBy", createdBy);
        await cmd.ExecuteNonQueryAsync(ct);

        var row = await GetTicketAsync(tenantId, id, ct);
        return row!;
    }

    public async Task<(IReadOnlyList<SupportTicketRow> Items, int Total)> ListTicketsAsync(
        Guid tenantId,
        Guid? reporterUserId,
        string? status,
        int page,
        int limit,
        CancellationToken ct = default)
    {
        var offset = Math.Max(0, (page - 1) * limit);
        var conn = await GetOpenConnectionAsync(ct);

        await using var countCmd = new NpgsqlCommand(
            """
            SELECT COUNT(*)
            FROM identity.support_tickets t
            WHERE t.tenant_id = @tenantId
              AND t.deleted_at IS NULL
              AND (@reporterUserId::uuid IS NULL OR t.reporter_user_id = @reporterUserId::uuid)
              AND (@status::text IS NULL OR t.status = @status::text)
            """,
            conn);
        countCmd.Parameters.AddWithValue("tenantId", tenantId);
        countCmd.Parameters.AddWithValue("reporterUserId", (object?)reporterUserId ?? DBNull.Value);
        countCmd.Parameters.AddWithValue("status", (object?)status ?? DBNull.Value);
        var total = Convert.ToInt32(await countCmd.ExecuteScalarAsync(ct), System.Globalization.CultureInfo.InvariantCulture);

        await using var cmd = new NpgsqlCommand(
            """
            SELECT t.id, t.tenant_id, tn.name, t.reporter_user_id,
                   u.email::text, p.full_name, t.assigned_to_user_id,
                   t.subject, t.body, t.category, t.status, t.created_at, t.row_version
            FROM identity.support_tickets t
            JOIN identity.users u ON u.id = t.reporter_user_id
            LEFT JOIN identity.profiles p ON p.user_id = u.id AND p.deleted_at IS NULL
            JOIN identity.tenants tn ON tn.id = t.tenant_id
            WHERE t.tenant_id = @tenantId
              AND t.deleted_at IS NULL
              AND (@reporterUserId::uuid IS NULL OR t.reporter_user_id = @reporterUserId::uuid)
              AND (@status::text IS NULL OR t.status = @status::text)
            ORDER BY t.created_at DESC
            LIMIT @limit OFFSET @offset
            """,
            conn);
        cmd.Parameters.AddWithValue("tenantId", tenantId);
        cmd.Parameters.AddWithValue("reporterUserId", (object?)reporterUserId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("status", (object?)status ?? DBNull.Value);
        cmd.Parameters.AddWithValue("limit", limit);
        cmd.Parameters.AddWithValue("offset", offset);

        var items = new List<SupportTicketRow>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            items.Add(MapTicket(reader));

        return (items, total);
    }

    public async Task<SupportTicketRow?> GetTicketAsync(
        Guid tenantId,
        Guid ticketId,
        CancellationToken ct = default)
    {
        var conn = await GetOpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(
            """
            SELECT t.id, t.tenant_id, tn.name, t.reporter_user_id,
                   u.email::text, p.full_name, t.assigned_to_user_id,
                   t.subject, t.body, t.category, t.status, t.created_at, t.row_version
            FROM identity.support_tickets t
            JOIN identity.users u ON u.id = t.reporter_user_id
            LEFT JOIN identity.profiles p ON p.user_id = u.id AND p.deleted_at IS NULL
            JOIN identity.tenants tn ON tn.id = t.tenant_id
            WHERE t.id = @ticketId
              AND t.tenant_id = @tenantId
              AND t.deleted_at IS NULL
            """,
            conn);
        cmd.Parameters.AddWithValue("ticketId", ticketId);
        cmd.Parameters.AddWithValue("tenantId", tenantId);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        return await reader.ReadAsync(ct) ? MapTicket(reader) : null;
    }

    public async Task<bool> UpdateTicketAsync(
        Guid tenantId,
        Guid ticketId,
        string? status,
        Guid? assignedToUserId,
        Guid updatedBy,
        CancellationToken ct = default)
    {
        var conn = await GetOpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(
            """
            UPDATE identity.support_tickets
            SET status = COALESCE(@status, status),
                assigned_to_user_id = COALESCE(@assignedToUserId, assigned_to_user_id),
                updated_at = now(),
                updated_by = @updatedBy
            WHERE id = @ticketId
              AND tenant_id = @tenantId
              AND deleted_at IS NULL
            """,
            conn);
        cmd.Parameters.AddWithValue("ticketId", ticketId);
        cmd.Parameters.AddWithValue("tenantId", tenantId);
        cmd.Parameters.AddWithValue("status", (object?)status ?? DBNull.Value);
        cmd.Parameters.AddWithValue("assignedToUserId", (object?)assignedToUserId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("updatedBy", updatedBy);

        return await cmd.ExecuteNonQueryAsync(ct) > 0;
    }

    private static SupportTicketRow MapTicket(NpgsqlDataReader reader) =>
        new(
            reader.GetGuid(0),
            reader.GetGuid(1),
            reader.GetString(2),
            reader.GetGuid(3),
            reader.GetString(4),
            reader.IsDBNull(5) ? null : reader.GetString(5),
            reader.IsDBNull(6) ? null : reader.GetGuid(6),
            reader.GetString(7),
            reader.GetString(8),
            reader.GetString(9),
            reader.GetString(10),
            reader.GetDateTime(11),
            reader.GetInt32(12));

    private async Task<NpgsqlConnection> GetOpenConnectionAsync(CancellationToken ct)
    {
        var conn = (NpgsqlConnection)db.Database.GetDbConnection();
        if (conn.State != System.Data.ConnectionState.Open)
            await conn.OpenAsync(ct);
        return conn;
    }
}
