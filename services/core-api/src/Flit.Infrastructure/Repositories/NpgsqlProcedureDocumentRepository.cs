using System.Data;
using System.Text.Json;
using Npgsql;
using NpgsqlTypes;
using Microsoft.EntityFrameworkCore;
using Flit.Infrastructure.Persistence;
using Flit.Modules.ProceduresConfig.Domain;
using Flit.Modules.ProceduresConfig.Ports;

namespace Flit.Infrastructure.Repositories;

public sealed class NpgsqlProcedureDocumentRepository(FlitDbContext db) : IProcedureDocumentRepository
{
    public async Task<GeneratedProcedureDocumentRecord> AddGeneratedAsync(
        Guid tenantId,
        Guid procedureInstanceId,
        Guid documentTypeId,
        Guid templateVersionId,
        Guid fileId,
        string dataSnapshotJson,
        Guid actorUserId,
        CancellationToken ct = default)
    {
        var id = Guid.NewGuid();
        var conn = (NpgsqlConnection)db.Database.GetDbConnection();
        if (conn.State != ConnectionState.Open)
            await conn.OpenAsync(ct);

        await TenantDbContext.SetTenantContextAsync(conn, tenantId, ct);

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO procedures.procedure_documents (
              id, tenant_id, procedure_instance_id, kind, document_type_id,
              file_id, template_version_id, data_snapshot, status,
              created_by, updated_by
            ) VALUES (
              @id, @tenant_id, @procedure_instance_id, 'auto_generated', @document_type_id,
              @file_id, @template_version_id, @data_snapshot::jsonb, 'generated',
              @actor, @actor
            )
            """;
        cmd.Parameters.Add(new NpgsqlParameter("id", NpgsqlDbType.Uuid) { Value = id });
        cmd.Parameters.Add(new NpgsqlParameter("tenant_id", NpgsqlDbType.Uuid) { Value = tenantId });
        cmd.Parameters.Add(new NpgsqlParameter("procedure_instance_id", NpgsqlDbType.Uuid) { Value = procedureInstanceId });
        cmd.Parameters.Add(new NpgsqlParameter("document_type_id", NpgsqlDbType.Uuid) { Value = documentTypeId });
        cmd.Parameters.Add(new NpgsqlParameter("file_id", NpgsqlDbType.Uuid) { Value = fileId });
        cmd.Parameters.Add(new NpgsqlParameter("template_version_id", NpgsqlDbType.Uuid) { Value = templateVersionId });
        cmd.Parameters.Add(new NpgsqlParameter("data_snapshot", NpgsqlDbType.Jsonb) { Value = dataSnapshotJson });
        cmd.Parameters.Add(new NpgsqlParameter("actor", NpgsqlDbType.Uuid) { Value = actorUserId });
        await cmd.ExecuteNonQueryAsync(ct);

        using var snapshotDoc = JsonDocument.Parse(dataSnapshotJson);
        return new GeneratedProcedureDocumentRecord(
            id,
            tenantId,
            procedureInstanceId,
            documentTypeId,
            templateVersionId,
            fileId,
            snapshotDoc.RootElement.Clone(),
            "generated");
    }

    public async Task<IReadOnlyList<GeneratedProcedureDocumentRecord>> ListByInstanceAsync(
        Guid tenantId,
        Guid procedureInstanceId,
        CancellationToken ct = default)
    {
        var conn = (NpgsqlConnection)db.Database.GetDbConnection();
        if (conn.State != ConnectionState.Open)
            await conn.OpenAsync(ct);

        await TenantDbContext.SetTenantContextAsync(conn, tenantId, ct);

        var list = new List<GeneratedProcedureDocumentRecord>();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT id, tenant_id, procedure_instance_id, document_type_id,
                   template_version_id, file_id, data_snapshot, status
            FROM procedures.procedure_documents
            WHERE tenant_id = @tenant_id
              AND procedure_instance_id = @instance_id
              AND deleted_at IS NULL
            """;
        cmd.Parameters.Add(new NpgsqlParameter("tenant_id", NpgsqlDbType.Uuid) { Value = tenantId });
        cmd.Parameters.Add(new NpgsqlParameter("instance_id", NpgsqlDbType.Uuid) { Value = procedureInstanceId });

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            using var snapshotDoc = JsonDocument.Parse(reader.GetString(6));
            list.Add(new GeneratedProcedureDocumentRecord(
                reader.GetGuid(0),
                reader.GetGuid(1),
                reader.GetGuid(2),
                reader.GetGuid(3),
                reader.IsDBNull(4) ? Guid.Empty : reader.GetGuid(4),
                reader.IsDBNull(5) ? Guid.Empty : reader.GetGuid(5),
                snapshotDoc.RootElement.Clone(),
                reader.GetString(7)));
        }

        return list;
    }
}
