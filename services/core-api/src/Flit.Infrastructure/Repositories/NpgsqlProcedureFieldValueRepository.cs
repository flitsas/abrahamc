using Microsoft.EntityFrameworkCore;
using Flit.Infrastructure.Persistence;
using Flit.Modules.Procedures.Ports;

namespace Flit.Infrastructure.Repositories;

public sealed class NpgsqlProcedureFieldValueRepository(FlitDbContext db) : IProcedureFieldValueRepository
{
    public async Task SaveManyAsync(IReadOnlyList<ProcedureFieldValueEntry> entries, CancellationToken ct = default)
    {
        if (entries.Count == 0)
        {
            return;
        }

        // No disponer la conexión: FlitDbContext la comparte entre repositorios del request.
        var conn = db.Database.GetDbConnection();
        if (conn.State != System.Data.ConnectionState.Open)
        {
            await conn.OpenAsync(ct);
        }

        foreach (var entry in entries)
        {
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = """
                UPDATE procedures.procedure_field_values SET
                  value = @value::jsonb,
                  updated_by = @updated_by,
                  updated_at = now()
                WHERE procedure_instance_id = @procedure_instance_id
                  AND field_key = @field_key
                  AND COALESCE(edge_role, '') = COALESCE(@edge_role, '')
                  AND deleted_at IS NULL;

                INSERT INTO procedures.procedure_field_values (
                  id, tenant_id, procedure_instance_id, edge_role, field_key, value, data_type,
                  created_by, updated_by, created_at, updated_at
                )
                SELECT
                  @id, @tenant_id, @procedure_instance_id, @edge_role, @field_key, @value::jsonb, @data_type,
                  @created_by, @updated_by, now(), now()
                WHERE NOT EXISTS (
                  SELECT 1 FROM procedures.procedure_field_values
                  WHERE procedure_instance_id = @procedure_instance_id
                    AND field_key = @field_key
                    AND COALESCE(edge_role, '') = COALESCE(@edge_role, '')
                    AND deleted_at IS NULL
                );
                """;

            Add(cmd, "id", entry.Id);
            Add(cmd, "tenant_id", entry.TenantId);
            Add(cmd, "procedure_instance_id", entry.ProcedureInstanceId);
            Add(cmd, "edge_role", (object?)entry.EdgeRole ?? DBNull.Value);
            Add(cmd, "field_key", entry.FieldKey);
            Add(cmd, "value", entry.ValueJson);
            Add(cmd, "data_type", entry.DataType);
            Add(cmd, "created_by", entry.CreatedBy);
            Add(cmd, "updated_by", entry.UpdatedBy);

            try
            {
                await cmd.ExecuteNonQueryAsync(ct);
            }
            catch (Exception ex) when (IsMissingTable(ex))
            {
                return;
            }
        }
    }

    private static void Add(System.Data.Common.DbCommand cmd, string name, object value)
    {
        var p = cmd.CreateParameter();
        p.ParameterName = name;
        p.Value = value;
        cmd.Parameters.Add(p);
    }

    private static bool IsMissingTable(Exception ex) =>
        ex.Message.Contains("procedure_field_values", StringComparison.OrdinalIgnoreCase);
}
