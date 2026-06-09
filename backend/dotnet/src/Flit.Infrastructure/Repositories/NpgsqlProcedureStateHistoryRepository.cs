using System.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using NpgsqlTypes;
using Flit.Infrastructure.Persistence;
using Flit.Modules.Procedures.Domain;
using Flit.Modules.Procedures.Ports;

namespace Flit.Infrastructure.Repositories;

public sealed class NpgsqlProcedureStateHistoryRepository(FlitDbContext db) : IProcedureStateHistoryRepository
{
    public async Task AppendAsync(ProcedureStateHistoryEntry entry, CancellationToken ct = default)
    {
        var conn = db.Database.GetDbConnection();
        if (conn.State != ConnectionState.Open)
        {
            await conn.OpenAsync(ct);
        }

        await TenantDbContext.SetTenantContextAsync(conn, entry.TenantId, ct);

        await using var cmd = (NpgsqlCommand)conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO procedures.procedure_state_history (
                id, tenant_id, procedure_instance_id, from_state, to_state,
                reason, changed_by, changed_at
            ) VALUES (
                @id, @tenant_id, @procedure_instance_id, @from_state, @to_state,
                @reason, @changed_by, @changed_at
            )
            """;

        AddUuid(cmd, "id", entry.Id);
        AddUuid(cmd, "tenant_id", entry.TenantId);
        AddUuid(cmd, "procedure_instance_id", entry.ProcedureInstanceId);
        AddNullableText(cmd, "from_state", entry.FromState);
        AddText(cmd, "to_state", entry.ToState);
        AddNullableText(cmd, "reason", entry.Reason);
        AddUuid(cmd, "changed_by", entry.ChangedBy);
        AddTimestampTz(cmd, "changed_at", entry.ChangedAt);

        try
        {
            await cmd.ExecuteNonQueryAsync(ct);
        }
        catch (PostgresException ex) when (ex.SqlState == "42P01")
        {
            // Tabla aún no migrada en entorno local — no bloquear DEV in-memory path.
        }
    }

    public async Task<IReadOnlyList<ProcedureStateHistoryEntry>> ListByInstanceAsync(
        Guid tenantId,
        Guid procedureInstanceId,
        CancellationToken ct = default)
    {
        var conn = db.Database.GetDbConnection();
        if (conn.State != ConnectionState.Open)
        {
            await conn.OpenAsync(ct);
        }

        await TenantDbContext.SetTenantContextAsync(conn, tenantId, ct);

        await using var cmd = (NpgsqlCommand)conn.CreateCommand();
        cmd.CommandText = """
            SELECT id, tenant_id, procedure_instance_id, from_state, to_state,
                   reason, changed_by, changed_at
            FROM procedures.procedure_state_history
            WHERE tenant_id = @tenant_id
              AND procedure_instance_id = @procedure_instance_id
            ORDER BY changed_at DESC
            """;

        AddUuid(cmd, "tenant_id", tenantId);
        AddUuid(cmd, "procedure_instance_id", procedureInstanceId);

        var list = new List<ProcedureStateHistoryEntry>();
        try
        {
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                list.Add(new ProcedureStateHistoryEntry(
                    reader.GetGuid(0),
                    reader.GetGuid(1),
                    reader.GetGuid(2),
                    reader.IsDBNull(3) ? null : reader.GetString(3),
                    reader.GetString(4),
                    reader.IsDBNull(5) ? null : reader.GetString(5),
                    reader.GetGuid(6),
                    reader.GetFieldValue<DateTimeOffset>(7)));
            }
        }
        catch (PostgresException ex) when (ex.SqlState == "42P01")
        {
            return list;
        }

        return list;
    }

    private static void AddUuid(NpgsqlCommand cmd, string name, Guid value) =>
        cmd.Parameters.Add(new NpgsqlParameter(name, NpgsqlDbType.Uuid) { Value = value });

    private static void AddText(NpgsqlCommand cmd, string name, string value) =>
        cmd.Parameters.Add(new NpgsqlParameter(name, NpgsqlDbType.Text) { Value = value });

    private static void AddNullableText(NpgsqlCommand cmd, string name, string? value) =>
        cmd.Parameters.Add(new NpgsqlParameter(name, NpgsqlDbType.Text)
        {
            Value = string.IsNullOrWhiteSpace(value) ? DBNull.Value : value,
        });

    private static void AddTimestampTz(NpgsqlCommand cmd, string name, DateTimeOffset value) =>
        cmd.Parameters.Add(new NpgsqlParameter(name, NpgsqlDbType.TimestampTz) { Value = value });
}
