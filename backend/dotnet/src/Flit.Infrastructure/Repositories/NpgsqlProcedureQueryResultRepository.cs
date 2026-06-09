using Microsoft.EntityFrameworkCore;
using Flit.Infrastructure.Persistence;
using Flit.Modules.Procedures.Ports;

namespace Flit.Infrastructure.Repositories;

public sealed class NpgsqlProcedureQueryResultRepository(FlitDbContext db) : IProcedureQueryResultRepository
{
    public async Task UpsertAsync(ProcedureQueryResultRecord record, CancellationToken ct = default)
    {
        await using var conn = db.Database.GetDbConnection();
        if (conn.State != System.Data.ConnectionState.Open)
        {
            await conn.OpenAsync(ct);
        }

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            UPDATE procedures.procedure_query_results SET
              status = @status,
              result = @result::jsonb,
              responded_at = @responded_at,
              integration_call_id = @integration_call_id,
              updated_by = @updated_by,
              updated_at = @responded_at
            WHERE procedure_instance_id = @procedure_instance_id
              AND query_connector_code = @query_connector_code
              AND COALESCE(edge_role, '') = COALESCE(@edge_role, '')
              AND deleted_at IS NULL;

            INSERT INTO procedures.procedure_query_results (
              id, tenant_id, procedure_instance_id, query_connector_code, edge_role,
              source, status, result, requested_at, responded_at, integration_call_id,
              created_by, updated_by, created_at, updated_at
            )
            SELECT
              @id, @tenant_id, @procedure_instance_id, @query_connector_code, @edge_role,
              @source, @status, @result::jsonb, @requested_at, @responded_at, @integration_call_id,
              @created_by, @updated_by, @requested_at, @responded_at
            WHERE NOT EXISTS (
              SELECT 1 FROM procedures.procedure_query_results
              WHERE procedure_instance_id = @procedure_instance_id
                AND query_connector_code = @query_connector_code
                AND COALESCE(edge_role, '') = COALESCE(@edge_role, '')
                AND deleted_at IS NULL
            );
            """;

        Add(cmd, "id", record.Id);
        Add(cmd, "tenant_id", record.TenantId);
        Add(cmd, "procedure_instance_id", record.ProcedureInstanceId);
        Add(cmd, "query_connector_code", record.QueryConnectorCode);
        Add(cmd, "edge_role", (object?)record.EdgeRole ?? DBNull.Value);
        Add(cmd, "source", record.Source);
        Add(cmd, "status", record.Status);
        Add(cmd, "result", record.Result.GetRawText());
        Add(cmd, "requested_at", record.RequestedAt.UtcDateTime);
        Add(cmd, "responded_at", (object?)record.RespondedAt?.UtcDateTime ?? DBNull.Value);
        Add(cmd, "integration_call_id", (object?)record.IntegrationCallId ?? DBNull.Value);
        Add(cmd, "created_by", record.CreatedBy);
        Add(cmd, "updated_by", record.UpdatedBy);

        try
        {
            await cmd.ExecuteNonQueryAsync(ct);
        }
        catch (Exception ex) when (IsMissingTable(ex))
        {
        }
    }

    public async Task<IReadOnlyList<ProcedureQueryResultRecord>> ListByInstanceAsync(
        Guid tenantId,
        Guid procedureInstanceId,
        CancellationToken ct = default)
    {
        await using var conn = db.Database.GetDbConnection();
        if (conn.State != System.Data.ConnectionState.Open)
        {
            await conn.OpenAsync(ct);
        }

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT id, tenant_id, procedure_instance_id, query_connector_code, edge_role,
                   source, status, result, requested_at, responded_at, integration_call_id,
                   created_by, updated_by
            FROM procedures.procedure_query_results
            WHERE tenant_id = @tenant_id
              AND procedure_instance_id = @procedure_instance_id
              AND deleted_at IS NULL
            ORDER BY query_connector_code
            """;

        Add(cmd, "tenant_id", tenantId);
        Add(cmd, "procedure_instance_id", procedureInstanceId);

        var list = new List<ProcedureQueryResultRecord>();
        try
        {
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                list.Add(new ProcedureQueryResultRecord(
                    reader.GetGuid(0),
                    reader.GetGuid(1),
                    reader.GetGuid(2),
                    reader.GetString(3),
                    reader.IsDBNull(4) ? null : reader.GetString(4),
                    reader.GetString(5),
                    reader.GetString(6),
                    System.Text.Json.JsonDocument.Parse(reader.GetString(7)).RootElement.Clone(),
                    new DateTimeOffset(DateTime.SpecifyKind(reader.GetDateTime(8), DateTimeKind.Utc)),
                    reader.IsDBNull(9)
                        ? null
                        : new DateTimeOffset(DateTime.SpecifyKind(reader.GetDateTime(9), DateTimeKind.Utc)),
                    reader.IsDBNull(10) ? null : reader.GetGuid(10),
                    reader.GetGuid(11),
                    reader.GetGuid(12)));
            }
        }
        catch (Exception ex) when (IsMissingTable(ex))
        {
        }

        return list;
    }

    private static void Add(System.Data.Common.DbCommand cmd, string name, object value)
    {
        var p = cmd.CreateParameter();
        p.ParameterName = name;
        p.Value = value;
        cmd.Parameters.Add(p);
    }

    private static bool IsMissingTable(Exception ex) =>
        ex.Message.Contains("procedure_query_results", StringComparison.OrdinalIgnoreCase);
}
