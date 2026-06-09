using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Flit.Infrastructure.Persistence;
using Flit.Modules.Integrations.Domain;
using Flit.Modules.Integrations.Ports;

namespace Flit.Infrastructure.Repositories;

public sealed class NpgsqlExternalQueryCallLogRepository(FlitDbContext db) : IExternalQueryCallLogRepository
{
    public async Task<ExternalQueryCallLogEntry?> FindCompletedByIdempotencyKeyAsync(
        Guid tenantId,
        string queryConnectorCode,
        string idempotencyKey,
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
                   request, response, http_status, latency_ms, succeeded, error_message, called_at
            FROM integrations.external_query_calls
            WHERE tenant_id = @tenant_id
              AND query_connector_code = @connector
              AND succeeded = true
              AND request #>> '{_flit,idempotency_key}' = @idempotency_key
            ORDER BY called_at DESC
            LIMIT 1
            """;

        Add(cmd, "tenant_id", tenantId);
        Add(cmd, "connector", queryConnectorCode.Trim());
        Add(cmd, "idempotency_key", idempotencyKey.Trim());

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
        {
            return null;
        }

        return MapRow(reader);
    }

    public async Task LogAsync(ExternalQueryCallLogEntry entry, CancellationToken ct = default)
    {
        await using var conn = db.Database.GetDbConnection();
        if (conn.State != System.Data.ConnectionState.Open)
        {
            await conn.OpenAsync(ct);
        }

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO integrations.external_query_calls (
              id, tenant_id, procedure_instance_id, query_connector_code, edge_role,
              request, response, http_status, latency_ms, succeeded, error_message, called_at
            ) VALUES (
              @id, @tenant_id, @procedure_instance_id, @query_connector_code, @edge_role,
              @request::jsonb, @response::jsonb, @http_status, @latency_ms, @succeeded, @error_message, @called_at
            )
            """;

        Add(cmd, "id", entry.Id);
        Add(cmd, "tenant_id", entry.TenantId);
        Add(cmd, "procedure_instance_id", (object?)entry.ProcedureInstanceId ?? DBNull.Value);
        Add(cmd, "query_connector_code", entry.QueryConnectorCode);
        Add(cmd, "edge_role", (object?)entry.EdgeRole ?? DBNull.Value);
        Add(cmd, "request", entry.Request.GetRawText());
        Add(cmd, "response", entry.Response?.GetRawText() ?? (object)DBNull.Value);
        Add(cmd, "http_status", (object?)entry.HttpStatus ?? DBNull.Value);
        Add(cmd, "latency_ms", (object?)entry.LatencyMs ?? DBNull.Value);
        Add(cmd, "succeeded", entry.Succeeded);
        Add(cmd, "error_message", (object?)entry.ErrorMessage ?? DBNull.Value);
        Add(cmd, "called_at", entry.CalledAt.UtcDateTime);

        try
        {
            await cmd.ExecuteNonQueryAsync(ct);
        }
        catch (Exception ex) when (IsMissingExternalQueryCalls(ex))
        {
            // Tabla aún no migrada en el ambiente.
        }
    }

    private static ExternalQueryCallLogEntry MapRow(System.Data.Common.DbDataReader r) =>
        new(
            r.GetGuid(0),
            r.GetGuid(1),
            r.IsDBNull(2) ? null : r.GetGuid(2),
            r.GetString(3),
            r.IsDBNull(4) ? null : r.GetString(4),
            JsonDocument.Parse(r.GetString(5)).RootElement.Clone(),
            r.IsDBNull(6) ? null : JsonDocument.Parse(r.GetString(6)).RootElement.Clone(),
            r.IsDBNull(7) ? null : r.GetInt32(7),
            r.IsDBNull(8) ? null : r.GetInt32(8),
            r.GetBoolean(9),
            r.IsDBNull(10) ? null : r.GetString(10),
            new DateTimeOffset(DateTime.SpecifyKind(r.GetDateTime(11), DateTimeKind.Utc)));

    private static void Add(System.Data.Common.DbCommand cmd, string name, object value)
    {
        var p = cmd.CreateParameter();
        p.ParameterName = name;
        p.Value = value;
        cmd.Parameters.Add(p);
    }

    private static bool IsMissingExternalQueryCalls(Exception ex) =>
        ex.Message.Contains("external_query_calls", StringComparison.OrdinalIgnoreCase);
}
