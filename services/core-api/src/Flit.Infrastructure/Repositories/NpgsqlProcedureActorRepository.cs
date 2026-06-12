using System.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using NpgsqlTypes;
using Flit.Infrastructure.Persistence;
using Flit.Modules.Procedures.Ports;

namespace Flit.Infrastructure.Repositories;

public sealed class NpgsqlProcedureActorRepository(FlitDbContext db) : IProcedureActorRepository
{
    public async Task UpsertAsync(ProcedureActorEntry entry, CancellationToken ct = default)
    {
        var conn = db.Database.GetDbConnection();
        if (conn.State != ConnectionState.Open)
        {
            await conn.OpenAsync(ct);
        }

        await TenantDbContext.SetTenantContextAsync(conn, entry.TenantId, ct);

        await using var cmd = (NpgsqlCommand)conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO procedures.procedure_actors (
                id, tenant_id, procedure_instance_id, edge_role, person_kind,
                document_type_id, document_number, full_name,
                ownership_percentage, owner_sequence,
                created_by, updated_by, created_at, updated_at
            ) VALUES (
                @id, @tenant_id, @procedure_instance_id, @edge_role, @person_kind,
                (SELECT id FROM catalogs.document_types WHERE code = @document_type_code LIMIT 1),
                @document_number, @full_name,
                @ownership_percentage, @owner_sequence,
                @created_by, @updated_by, now(), now()
            )
            ON CONFLICT (procedure_instance_id, edge_role, owner_sequence) DO UPDATE SET
                person_kind = EXCLUDED.person_kind,
                document_type_id = EXCLUDED.document_type_id,
                document_number = EXCLUDED.document_number,
                full_name = EXCLUDED.full_name,
                ownership_percentage = EXCLUDED.ownership_percentage,
                updated_by = EXCLUDED.updated_by,
                updated_at = now()
            """;

        BindActorParameters(cmd, entry);

        try
        {
            await cmd.ExecuteNonQueryAsync(ct);
        }
        catch (PostgresException ex) when (ex.SqlState is "42P01" or "23503")
        {
        }
    }

    public async Task ReplaceOwnersAsync(
        Guid tenantId,
        Guid procedureInstanceId,
        string edgeRole,
        IReadOnlyList<ProcedureActorEntry> owners,
        CancellationToken ct = default)
    {
        var conn = db.Database.GetDbConnection();
        if (conn.State != ConnectionState.Open)
        {
            await conn.OpenAsync(ct);
        }

        await TenantDbContext.SetTenantContextAsync(conn, tenantId, ct);

        await using var deleteCmd = (NpgsqlCommand)conn.CreateCommand();
        deleteCmd.CommandText = """
            UPDATE procedures.procedure_actors
            SET deleted_at = now(), updated_at = now()
            WHERE tenant_id = @tenant_id
              AND procedure_instance_id = @procedure_instance_id
              AND edge_role = @edge_role
              AND deleted_at IS NULL
            """;
        AddUuid(deleteCmd, "tenant_id", tenantId);
        AddUuid(deleteCmd, "procedure_instance_id", procedureInstanceId);
        AddText(deleteCmd, "edge_role", edgeRole);

        try
        {
            await deleteCmd.ExecuteNonQueryAsync(ct);
        }
        catch (PostgresException ex) when (ex.SqlState == "42P01")
        {
            return;
        }

        foreach (var owner in owners)
        {
            await UpsertAsync(owner, ct);
        }
    }

    public async Task<ProcedureActorEntry?> GetByInstanceAndEdgeAsync(
        Guid tenantId,
        Guid procedureInstanceId,
        string edgeRole,
        CancellationToken ct = default)
    {
        var all = await ListByInstanceAndEdgeAsync(tenantId, procedureInstanceId, edgeRole, ct);
        return all.Count > 0 ? all[0] : null;
    }

    public async Task<IReadOnlyList<ProcedureActorEntry>> ListByInstanceAndEdgeAsync(
        Guid tenantId,
        Guid procedureInstanceId,
        string edgeRole,
        CancellationToken ct = default)
    {
        var all = await ListByInstanceAsync(tenantId, procedureInstanceId, ct);
        return all
            .Where(a => string.Equals(a.EdgeRole, edgeRole, StringComparison.OrdinalIgnoreCase))
            .OrderBy(a => a.OwnerSequence)
            .ToList();
    }

    public async Task<IReadOnlyList<ProcedureActorEntry>> ListByInstanceAsync(
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
            SELECT a.id, a.tenant_id, a.procedure_instance_id, a.edge_role, a.person_kind,
                   dt.code, a.document_number, a.full_name,
                   a.ownership_percentage, a.owner_sequence,
                   a.created_by, a.updated_by
            FROM procedures.procedure_actors a
            JOIN catalogs.document_types dt ON dt.id = a.document_type_id
            WHERE a.tenant_id = @tenant_id
              AND a.procedure_instance_id = @procedure_instance_id
              AND a.deleted_at IS NULL
            ORDER BY a.edge_role, a.owner_sequence
            """;

        AddUuid(cmd, "tenant_id", tenantId);
        AddUuid(cmd, "procedure_instance_id", procedureInstanceId);

        var list = new List<ProcedureActorEntry>();
        try
        {
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                list.Add(MapRow(reader));
            }
        }
        catch (PostgresException ex) when (ex.SqlState == "42P01")
        {
            return list;
        }

        return list;
    }

    private static ProcedureActorEntry MapRow(NpgsqlDataReader reader) => new(
        reader.GetGuid(0),
        reader.GetGuid(1),
        reader.GetGuid(2),
        reader.GetString(3),
        reader.GetString(4),
        reader.GetString(5),
        reader.GetString(6),
        reader.IsDBNull(7) ? null : reader.GetString(7),
        reader.IsDBNull(8) ? null : reader.GetDecimal(8),
        reader.GetInt16(9),
        reader.GetGuid(10),
        reader.GetGuid(11));

    private static void BindActorParameters(NpgsqlCommand cmd, ProcedureActorEntry entry)
    {
        AddUuid(cmd, "id", entry.Id);
        AddUuid(cmd, "tenant_id", entry.TenantId);
        AddUuid(cmd, "procedure_instance_id", entry.ProcedureInstanceId);
        AddText(cmd, "edge_role", entry.EdgeRole);
        AddText(cmd, "person_kind", entry.PersonKind);
        AddText(cmd, "document_type_code", entry.DocumentTypeCode);
        AddText(cmd, "document_number", entry.DocumentNumber);
        AddNullableText(cmd, "full_name", entry.FullName);
        AddNullableNumeric(cmd, "ownership_percentage", entry.OwnershipPercentage);
        Add(cmd, "owner_sequence", NpgsqlDbType.Smallint, entry.OwnerSequence);
        AddUuid(cmd, "created_by", entry.CreatedBy);
        AddUuid(cmd, "updated_by", entry.UpdatedBy);
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

    private static void AddNullableNumeric(NpgsqlCommand cmd, string name, decimal? value) =>
        cmd.Parameters.Add(new NpgsqlParameter(name, NpgsqlDbType.Numeric)
        {
            Value = value.HasValue ? value.Value : DBNull.Value,
        });

    private static void Add(NpgsqlCommand cmd, string name, NpgsqlDbType type, object value) =>
        cmd.Parameters.Add(new NpgsqlParameter(name, type) { Value = value });
}
