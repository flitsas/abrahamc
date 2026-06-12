using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Flit.Infrastructure.Persistence;
using Flit.Modules.ProceduresConfig.Domain;
using Flit.Modules.ProceduresConfig.Ports;

namespace Flit.Infrastructure.Repositories;

public sealed class NpgsqlEndpointCatalogRepository(FlitDbContext db) : IEndpointCatalogRepository
{
    public async Task<IReadOnlyList<EndpointCatalogRecord>> ListByTenantAsync(
        Guid tenantId,
        CancellationToken ct = default)
    {
        return await QueryAsync(
            """
            SELECT id, tenant_id, code, name, url, method, auth_type, auth_config::text,
                   field_mapping::text, timeout_ms, is_active, row_version, created_at, updated_at
            FROM procedures_config.endpoint_catalog
            WHERE tenant_id = @tenant_id AND deleted_at IS NULL
            ORDER BY code ASC
            """,
            cmd => Add(cmd, "tenant_id", tenantId),
            ct);
    }

    public async Task<EndpointCatalogRecord?> GetByIdAsync(
        Guid tenantId,
        Guid id,
        CancellationToken ct = default)
    {
        var list = await QueryAsync(
            """
            SELECT id, tenant_id, code, name, url, method, auth_type, auth_config::text,
                   field_mapping::text, timeout_ms, is_active, row_version, created_at, updated_at
            FROM procedures_config.endpoint_catalog
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

    public async Task<EndpointCatalogRecord?> GetByCodeAsync(
        Guid tenantId,
        string code,
        CancellationToken ct = default)
    {
        var list = await QueryAsync(
            """
            SELECT id, tenant_id, code, name, url, method, auth_type, auth_config::text,
                   field_mapping::text, timeout_ms, is_active, row_version, created_at, updated_at
            FROM procedures_config.endpoint_catalog
            WHERE tenant_id = @tenant_id AND code = @code AND deleted_at IS NULL
            """,
            cmd =>
            {
                Add(cmd, "tenant_id", tenantId);
                Add(cmd, "code", code);
            },
            ct);

        return list.Count > 0 ? list[0] : null;
    }

    public async Task<EndpointCatalogRecord> AddAsync(
        EndpointCatalogWriteModel model,
        CancellationToken ct = default)
    {
        var id = Guid.NewGuid();
        await using var conn = await OpenAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO procedures_config.endpoint_catalog (
              id, tenant_id, code, name, url, method, auth_type, auth_config, field_mapping,
              timeout_ms, is_active, created_by, updated_by
            ) VALUES (
              @id, @tenant_id, @code, @name, @url, @method, @auth_type, @auth_config::jsonb,
              @field_mapping::jsonb, @timeout_ms, @is_active, @actor, @actor
            )
            RETURNING id, tenant_id, code, name, url, method, auth_type, auth_config::text,
                      field_mapping::text, timeout_ms, is_active, row_version, created_at, updated_at
            """;

        Add(cmd, "id", id);
        Add(cmd, "tenant_id", model.TenantId);
        Add(cmd, "code", model.Code);
        Add(cmd, "name", model.Name);
        Add(cmd, "url", model.Url);
        Add(cmd, "method", model.Method);
        Add(cmd, "auth_type", model.AuthType);
        Add(cmd, "auth_config", model.AuthConfigJson);
        Add(cmd, "field_mapping", model.FieldMappingJson);
        Add(cmd, "timeout_ms", model.TimeoutMs);
        Add(cmd, "is_active", model.IsActive);
        Add(cmd, "actor", model.ActorUserId);

        try
        {
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            if (!await reader.ReadAsync(ct))
            {
                throw new InvalidOperationException("INSERT endpoint_catalog no devolvió fila.");
            }

            return ReadRow(reader);
        }
        catch (Exception ex) when (IsUniqueViolation(ex))
        {
            throw new InvalidOperationException("duplicate key: uq_endpoint_catalog_tenant_code", ex);
        }
    }

    public async Task<EndpointCatalogRecord?> UpdateAsync(
        EndpointCatalogWriteModel model,
        CancellationToken ct = default)
    {
        await using var conn = await OpenAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            UPDATE procedures_config.endpoint_catalog
            SET name = @name,
                url = @url,
                method = @method,
                auth_type = @auth_type,
                auth_config = @auth_config::jsonb,
                field_mapping = @field_mapping::jsonb,
                timeout_ms = @timeout_ms,
                is_active = @is_active,
                updated_by = @actor,
                updated_at = now()
            WHERE tenant_id = @tenant_id
              AND id = @id
              AND deleted_at IS NULL
              AND (@expected_row_version IS NULL OR row_version = @expected_row_version)
            RETURNING id, tenant_id, code, name, url, method, auth_type, auth_config::text,
                      field_mapping::text, timeout_ms, is_active, row_version, created_at, updated_at
            """;

        Add(cmd, "tenant_id", model.TenantId);
        Add(cmd, "id", model.Id!.Value);
        Add(cmd, "name", model.Name);
        Add(cmd, "url", model.Url);
        Add(cmd, "method", model.Method);
        Add(cmd, "auth_type", model.AuthType);
        Add(cmd, "auth_config", model.AuthConfigJson);
        Add(cmd, "field_mapping", model.FieldMappingJson);
        Add(cmd, "timeout_ms", model.TimeoutMs);
        Add(cmd, "is_active", model.IsActive);
        Add(cmd, "actor", model.ActorUserId);
        Add(cmd, "expected_row_version", (object?)model.ExpectedRowVersion ?? DBNull.Value);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
        {
            return null;
        }

        return ReadRow(reader);
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
            UPDATE procedures_config.endpoint_catalog
            SET deleted_at = now(), deleted_by = @deleted_by
            WHERE tenant_id = @tenant_id AND id = @id AND deleted_at IS NULL
            """;

        Add(cmd, "tenant_id", tenantId);
        Add(cmd, "id", id);
        Add(cmd, "deleted_by", deletedBy);

        var rows = await cmd.ExecuteNonQueryAsync(ct);
        return rows > 0;
    }

    private async Task<IReadOnlyList<EndpointCatalogRecord>> QueryAsync(
        string sql,
        Action<System.Data.Common.DbCommand> bind,
        CancellationToken ct)
    {
        await using var conn = await OpenAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        bind(cmd);

        var list = new List<EndpointCatalogRecord>();
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

    private static EndpointCatalogRecord ReadRow(System.Data.Common.DbDataReader reader)
    {
        using var authDoc = JsonDocument.Parse(reader.GetString(7));
        using var mappingDoc = JsonDocument.Parse(reader.GetString(8));
        return new EndpointCatalogRecord(
            reader.GetGuid(0),
            reader.GetGuid(1),
            reader.GetString(2),
            reader.GetString(3),
            reader.GetString(4),
            reader.GetString(5),
            reader.GetString(6),
            authDoc.RootElement.Clone(),
            mappingDoc.RootElement.Clone(),
            reader.GetInt32(9),
            reader.GetBoolean(10),
            reader.GetInt32(11),
            new DateTimeOffset(reader.GetDateTime(12), TimeSpan.Zero),
            new DateTimeOffset(reader.GetDateTime(13), TimeSpan.Zero));
    }

    private static void Add(System.Data.Common.DbCommand cmd, string name, object value)
    {
        var p = cmd.CreateParameter();
        p.ParameterName = name;
        p.Value = value;
        cmd.Parameters.Add(p);
    }

    private static bool IsUniqueViolation(Exception ex) =>
        ex.Message.Contains("uq_endpoint_catalog_tenant_code", StringComparison.OrdinalIgnoreCase) ||
        ex.Message.Contains("duplicate key", StringComparison.OrdinalIgnoreCase);
}
