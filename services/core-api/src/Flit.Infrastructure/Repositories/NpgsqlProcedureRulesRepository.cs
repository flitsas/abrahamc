using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Flit.Infrastructure.Persistence;
using Flit.Modules.ProceduresConfig.Domain;
using Flit.Modules.ProceduresConfig.Ports;

namespace Flit.Infrastructure.Repositories;

public sealed class NpgsqlProcedureRulesRepository(FlitDbContext db) : IProcedureRulesRepository
{
    public async Task<IReadOnlyList<ProcedureRuleRecord>> ListActiveForEvaluationAsync(
        Guid tenantId,
        Guid procedureTypeId,
        CancellationToken ct = default)
    {
        await using var conn = db.Database.GetDbConnection();
        if (conn.State != System.Data.ConnectionState.Open)
        {
            await conn.OpenAsync(ct);
        }

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT id, name, priority, condition_tree::text, actions::text
            FROM procedures_config.rules
            WHERE tenant_id = @tenant_id
              AND procedure_type_id = @procedure_type_id
              AND deleted_at IS NULL
              AND is_active = TRUE
            ORDER BY priority ASC, name ASC
            """;

        var tenantParam = cmd.CreateParameter();
        tenantParam.ParameterName = "tenant_id";
        tenantParam.Value = tenantId;
        cmd.Parameters.Add(tenantParam);

        var typeParam = cmd.CreateParameter();
        typeParam.ParameterName = "procedure_type_id";
        typeParam.Value = procedureTypeId;
        cmd.Parameters.Add(typeParam);

        var list = new List<ProcedureRuleRecord>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var id = reader.GetGuid(0);
            var name = reader.GetString(1);
            var priority = reader.GetInt32(2);
            var conditionJson = reader.GetString(3);
            var actionsJson = reader.GetString(4);

            using var condDoc = JsonDocument.Parse(conditionJson);
            using var actDoc = JsonDocument.Parse(actionsJson);

            list.Add(new ProcedureRuleRecord(
                id,
                name,
                priority,
                condDoc.RootElement.Clone(),
                actDoc.RootElement.Clone()));
        }

        return list;
    }
}
