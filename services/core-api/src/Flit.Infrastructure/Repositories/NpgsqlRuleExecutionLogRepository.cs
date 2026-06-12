using Microsoft.EntityFrameworkCore;
using Flit.Infrastructure.Persistence;
using Flit.Modules.ProceduresConfig.Ports;

namespace Flit.Infrastructure.Repositories;

public sealed class NpgsqlRuleExecutionLogRepository(FlitDbContext db) : IRuleExecutionLogRepository
{
    public async Task LogAsync(RuleExecutionLogEntry entry, CancellationToken ct = default)
    {
        await using var conn = db.Database.GetDbConnection();
        if (conn.State != System.Data.ConnectionState.Open)
        {
            await conn.OpenAsync(ct);
        }

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO procedures_config.rule_execution_logs (
              tenant_id, procedure_type_id, procedure_instance_id,
              payload, matched_rules, result, evaluated_at
            ) VALUES (
              @tenant_id, @procedure_type_id, @procedure_instance_id,
              @payload::jsonb, @matched_rules::jsonb, @result::jsonb, @evaluated_at
            )
            """;

        Add(cmd, "tenant_id", entry.TenantId);
        Add(cmd, "procedure_type_id", entry.ProcedureTypeId);
        Add(cmd, "procedure_instance_id", (object?)entry.ProcedureInstanceId ?? DBNull.Value);
        Add(cmd, "payload", entry.Payload.GetRawText());
        Add(cmd, "matched_rules", entry.MatchedRules.GetRawText());
        Add(cmd, "result", entry.Result.GetRawText());
        Add(cmd, "evaluated_at", entry.EvaluatedAt.UtcDateTime);

        try
        {
            await cmd.ExecuteNonQueryAsync(ct);
        }
        catch (Exception ex) when (IsMissingRuleExecutionLogs(ex))
        {
            // Tabla aún no migrada — no bloquea evaluación de reglas.
        }
    }

    private static void Add(System.Data.Common.DbCommand cmd, string name, object value)
    {
        var p = cmd.CreateParameter();
        p.ParameterName = name;
        p.Value = value;
        cmd.Parameters.Add(p);
    }

    private static bool IsMissingRuleExecutionLogs(Exception ex) =>
        ex.Message.Contains("rule_execution_logs", StringComparison.OrdinalIgnoreCase);
}
