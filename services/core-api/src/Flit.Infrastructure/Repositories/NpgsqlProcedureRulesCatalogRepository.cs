using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Flit.Infrastructure.Persistence;
using Flit.Modules.ProceduresConfig.Domain;
using Flit.Modules.ProceduresConfig.Ports;

namespace Flit.Infrastructure.Repositories;

public sealed class NpgsqlProcedureRulesCatalogRepository(FlitDbContext db) : IProcedureRulesCatalogRepository
{
    public async Task<IReadOnlyList<ProcedureRuleCatalogRecord>> ListByProcedureTypeAsync(
        Guid tenantId,
        Guid procedureTypeId,
        CancellationToken ct = default)
    {
        return await QueryAsync(
            """
            SELECT id, tenant_id, procedure_type_id, name, description,
                   condition_tree::text, actions::text, priority, is_active,
                   row_version, created_at, updated_at
            FROM procedures_config.rules
            WHERE tenant_id = @tenant_id
              AND procedure_type_id = @procedure_type_id
              AND deleted_at IS NULL
            ORDER BY priority ASC, name ASC
            """,
            cmd =>
            {
                Add(cmd, "tenant_id", tenantId);
                Add(cmd, "procedure_type_id", procedureTypeId);
            },
            ct);
    }

    public async Task<ProcedureRuleCatalogRecord?> GetByIdAsync(
        Guid tenantId,
        Guid id,
        CancellationToken ct = default)
    {
        var list = await QueryAsync(
            """
            SELECT id, tenant_id, procedure_type_id, name, description,
                   condition_tree::text, actions::text, priority, is_active,
                   row_version, created_at, updated_at
            FROM procedures_config.rules
            WHERE tenant_id = @tenant_id AND id = @id AND deleted_at IS NULL
            """,
            cmd =>
            {
                Add(cmd, "tenant_id", tenantId);
                Add(cmd, "id", id);
            },
            ct);

        return list.Count > 0 ? list[0] : null;
    }

    public async Task<ProcedureRuleCatalogRecord> AddAsync(
        ProcedureRuleWriteModel model,
        CancellationToken ct = default)
    {
        var id = Guid.NewGuid();
        await using var conn = await OpenAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO procedures_config.rules (
              id, tenant_id, procedure_type_id, name, description,
              condition_tree, actions, priority, is_active, created_by, updated_by
            ) VALUES (
              @id, @tenant_id, @procedure_type_id, @name, @description,
              @condition_tree::jsonb, @actions::jsonb, @priority, @is_active, @actor, @actor
            )
            RETURNING id, tenant_id, procedure_type_id, name, description,
                      condition_tree::text, actions::text, priority, is_active,
                      row_version, created_at, updated_at
            """;

        Add(cmd, "id", id);
        Add(cmd, "tenant_id", model.TenantId);
        Add(cmd, "procedure_type_id", model.ProcedureTypeId);
        Add(cmd, "name", model.Name);
        Add(cmd, "description", (object?)model.Description ?? DBNull.Value);
        Add(cmd, "condition_tree", model.ConditionTreeJson);
        Add(cmd, "actions", model.ActionsJson);
        Add(cmd, "priority", model.Priority);
        Add(cmd, "is_active", model.IsActive);
        Add(cmd, "actor", model.ActorUserId);

        try
        {
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            if (!await reader.ReadAsync(ct))
            {
                throw new InvalidOperationException("INSERT rules no devolvió fila.");
            }

            return ReadRow(reader);
        }
        catch (Exception ex) when (IsUniqueViolation(ex))
        {
            throw new InvalidOperationException("duplicate key: uq_rules_tenant_type_name", ex);
        }
    }

    public async Task<ProcedureRuleCatalogRecord?> UpdateAsync(
        ProcedureRuleWriteModel model,
        CancellationToken ct = default)
    {
        if (model.Id is null)
        {
            return null;
        }

        await using var conn = await OpenAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            UPDATE procedures_config.rules
            SET name = @name,
                description = @description,
                condition_tree = @condition_tree::jsonb,
                actions = @actions::jsonb,
                priority = @priority,
                is_active = @is_active,
                updated_by = @actor,
                updated_at = now()
            WHERE tenant_id = @tenant_id
              AND id = @id
              AND deleted_at IS NULL
              AND row_version = @row_version
            RETURNING id, tenant_id, procedure_type_id, name, description,
                      condition_tree::text, actions::text, priority, is_active,
                      row_version, created_at, updated_at
            """;

        Add(cmd, "tenant_id", model.TenantId);
        Add(cmd, "id", model.Id.Value);
        Add(cmd, "name", model.Name);
        Add(cmd, "description", (object?)model.Description ?? DBNull.Value);
        Add(cmd, "condition_tree", model.ConditionTreeJson);
        Add(cmd, "actions", model.ActionsJson);
        Add(cmd, "priority", model.Priority);
        Add(cmd, "is_active", model.IsActive);
        Add(cmd, "actor", model.ActorUserId);
        Add(cmd, "row_version", model.ExpectedRowVersion ?? 0);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        return await reader.ReadAsync(ct) ? ReadRow(reader) : null;
    }

    public async Task<bool> SoftDeleteAsync(
        Guid tenantId,
        Guid id,
        Guid deletedBy,
        CancellationToken ct = default)
    {
        await using var conn = await OpenAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            UPDATE procedures_config.rules
            SET deleted_at = now(), deleted_by = @deleted_by, is_active = FALSE
            WHERE tenant_id = @tenant_id AND id = @id AND deleted_at IS NULL
            """;

        Add(cmd, "tenant_id", tenantId);
        Add(cmd, "id", id);
        Add(cmd, "deleted_by", deletedBy);

        var rows = await cmd.ExecuteNonQueryAsync(ct);
        return rows > 0;
    }

    private async Task<IReadOnlyList<ProcedureRuleCatalogRecord>> QueryAsync(
        string sql,
        Action<System.Data.Common.DbCommand> bind,
        CancellationToken ct)
    {
        await using var conn = await OpenAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        bind(cmd);

        var list = new List<ProcedureRuleCatalogRecord>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            list.Add(ReadRow(reader));
        }

        return list;
    }

    private async Task<System.Data.Common.DbConnection> OpenAsync(CancellationToken ct)
    {
        var conn = db.Database.GetDbConnection();
        if (conn.State != System.Data.ConnectionState.Open)
        {
            await conn.OpenAsync(ct);
        }

        return conn;
    }

    private static ProcedureRuleCatalogRecord ReadRow(System.Data.Common.DbDataReader reader)
    {
        using var condDoc = JsonDocument.Parse(reader.GetString(5));
        using var actDoc = JsonDocument.Parse(reader.GetString(6));
        return new ProcedureRuleCatalogRecord(
            reader.GetGuid(0),
            reader.GetGuid(1),
            reader.GetGuid(2),
            reader.GetString(3),
            reader.IsDBNull(4) ? null : reader.GetString(4),
            condDoc.RootElement.Clone(),
            actDoc.RootElement.Clone(),
            reader.GetInt32(7),
            reader.GetBoolean(8),
            reader.GetInt32(9),
            new DateTimeOffset(reader.GetDateTime(10), TimeSpan.Zero),
            new DateTimeOffset(reader.GetDateTime(11), TimeSpan.Zero));
    }

    private static void Add(System.Data.Common.DbCommand cmd, string name, object value)
    {
        var p = cmd.CreateParameter();
        p.ParameterName = name;
        p.Value = value;
        cmd.Parameters.Add(p);
    }

    private static bool IsUniqueViolation(Exception ex) =>
        ex.Message.Contains("uq_rules_tenant_type_name", StringComparison.OrdinalIgnoreCase) ||
        ex.Message.Contains("duplicate key", StringComparison.OrdinalIgnoreCase);
}
