using Microsoft.EntityFrameworkCore;
using Flit.Infrastructure.Persistence;
using Flit.Modules.ProceduresConfig.Ports;

namespace Flit.Infrastructure.Repositories;

public sealed class NpgsqlEndpointCallLogRepository(FlitDbContext db) : IEndpointCallLogRepository
{
    public async Task LogAsync(EndpointCallLogEntry entry, CancellationToken ct = default)
    {
        await using var conn = db.Database.GetDbConnection();
        if (conn.State != System.Data.ConnectionState.Open)
        {
            await conn.OpenAsync(ct);
        }

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO integrations.endpoint_call_log (
              tenant_id, endpoint_code, procedure_instance_id,
              request, response, http_status, succeeded, called_at
            ) VALUES (
              @tenant_id, @endpoint_code, @procedure_instance_id,
              @request::jsonb, @response::jsonb, @http_status, @succeeded, @called_at
            )
            """;

        Add(cmd, "tenant_id", entry.TenantId);
        Add(cmd, "endpoint_code", entry.EndpointCode);
        Add(cmd, "procedure_instance_id", (object?)entry.ProcedureInstanceId ?? DBNull.Value);
        Add(cmd, "request", entry.Request.GetRawText());
        Add(cmd, "response", entry.Response?.GetRawText() ?? (object)DBNull.Value);
        Add(cmd, "http_status", (object?)entry.HttpStatus ?? DBNull.Value);
        Add(cmd, "succeeded", entry.Succeeded);
        Add(cmd, "called_at", entry.CalledAt.UtcDateTime);

        try
        {
            await cmd.ExecuteNonQueryAsync(ct);
        }
        catch (Exception ex) when (IsMissingEndpointCallLog(ex))
        {
            // Tabla aún no migrada en el ambiente — no bloquea evaluación de reglas.
        }
    }

    private static void Add(System.Data.Common.DbCommand cmd, string name, object value)
    {
        var p = cmd.CreateParameter();
        p.ParameterName = name;
        p.Value = value;
        cmd.Parameters.Add(p);
    }

    private static bool IsMissingEndpointCallLog(Exception ex) =>
        ex.Message.Contains("endpoint_call_log", StringComparison.OrdinalIgnoreCase) ||
        ex.Message.Contains("integrations", StringComparison.OrdinalIgnoreCase);
}
