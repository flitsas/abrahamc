using System.Data;
using Npgsql;
using NpgsqlTypes;
using Microsoft.EntityFrameworkCore;
using Flit.Infrastructure.Persistence;
using Flit.Modules.Procedures.Domain;

namespace Flit.Infrastructure.Repositories;

/// <summary>
/// Repositorio Npgsql para <c>procedures.procedure_instances</c>.
/// TRA-02 #9434 — persistencia de instancias radicadas con <c>config_snapshot</c> inmutable (ADR-0010).
/// Sigue el mismo patrón de conexión/RLS que <see cref="NpgsqlProceduresConfigReadRepository"/>.
/// </summary>
public sealed class NpgsqlProcedureInstanceRepository(FlitDbContext db) : IProcedureInstanceRepository
{
    public async Task<ProcedureInstance?> GetByIdAsync(
        Guid id, Guid tenantId, CancellationToken ct = default)
    {
        var conn = db.Database.GetDbConnection();
        if (conn.State != ConnectionState.Open)
            await conn.OpenAsync(ct);

        await TenantDbContext.SetTenantContextAsync(conn, tenantId, ct);

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT id, tenant_id, procedure_type_id, traffic_agency_id,
                   reference_number, state, config_snapshot::text,
                   config_schema_version, assigned_to_user_id,
                   radicated_at, total_amount, currency_code,
                   created_at, created_by, updated_at, updated_by, row_version
            FROM procedures.procedure_instances
            WHERE id = @id
              AND tenant_id = @tenant_id
              AND deleted_at IS NULL
            LIMIT 1
            """;

        AddUuid(cmd, "id", id);
        AddUuid(cmd, "tenant_id", tenantId);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        return await reader.ReadAsync(ct) ? MapRow(reader) : null;
    }

    public async Task<IReadOnlyList<ProcedureInstance>> ListByTenantAsync(
        Guid tenantId, CancellationToken ct = default)
    {
        var conn = db.Database.GetDbConnection();
        if (conn.State != ConnectionState.Open)
            await conn.OpenAsync(ct);

        await TenantDbContext.SetTenantContextAsync(conn, tenantId, ct);

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT id, tenant_id, procedure_type_id, traffic_agency_id,
                   reference_number, state, config_snapshot::text,
                   config_schema_version, assigned_to_user_id,
                   radicated_at, total_amount, currency_code,
                   created_at, created_by, updated_at, updated_by, row_version
            FROM procedures.procedure_instances
            WHERE tenant_id = @tenant_id
              AND deleted_at IS NULL
            ORDER BY created_at DESC
            """;

        AddUuid(cmd, "tenant_id", tenantId);

        var list = new List<ProcedureInstance>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            list.Add(MapRow(reader));

        return list;
    }

    public async Task SaveAsync(ProcedureInstance instance, CancellationToken ct = default)
    {
        var conn = db.Database.GetDbConnection();
        if (conn.State != ConnectionState.Open)
            await conn.OpenAsync(ct);

        await TenantDbContext.SetTenantContextAsync(conn, instance.TenantId, ct);

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO procedures.procedure_instances (
                id, tenant_id, procedure_type_id, traffic_agency_id,
                reference_number, state, config_snapshot, config_schema_version,
                assigned_to_user_id, radicated_at, total_amount, currency_code,
                created_at, created_by, updated_at, updated_by, row_version
            ) VALUES (
                @id, @tenant_id, @procedure_type_id, @traffic_agency_id,
                @reference_number, @state, @config_snapshot::jsonb, @config_schema_version,
                @assigned_to_user_id, @radicated_at, @total_amount, @currency_code,
                @created_at, @created_by, @updated_at, @updated_by, @row_version
            )
            ON CONFLICT (id) DO UPDATE SET
                state = EXCLUDED.state,
                assigned_to_user_id = EXCLUDED.assigned_to_user_id,
                updated_at = EXCLUDED.updated_at,
                updated_by = EXCLUDED.updated_by,
                row_version = EXCLUDED.row_version
            """;

        AddUuid(cmd, "id", instance.Id);
        AddUuid(cmd, "tenant_id", instance.TenantId);
        AddUuid(cmd, "procedure_type_id", instance.ProcedureTypeId);
        AddNullableUuid(cmd, "traffic_agency_id", instance.TrafficAgencyId);
        Add(cmd, "reference_number", NpgsqlDbType.Text, instance.ReferenceNumber);
        Add(cmd, "state", NpgsqlDbType.Text, instance.State);
        Add(cmd, "config_snapshot", NpgsqlDbType.Text, instance.ConfigSnapshot);
        Add(cmd, "config_schema_version", NpgsqlDbType.Integer, instance.ConfigSchemaVersion);
        AddNullableUuid(cmd, "assigned_to_user_id", instance.AssignedToUserId);
        AddNullableTimestampTz(cmd, "radicated_at", instance.RadicatedAt);
        Add(cmd, "total_amount", NpgsqlDbType.Numeric, instance.TotalAmount);
        Add(cmd, "currency_code", NpgsqlDbType.Char, instance.CurrencyCode);
        Add(cmd, "created_at", NpgsqlDbType.TimestampTz, instance.CreatedAt);
        AddUuid(cmd, "created_by", instance.CreatedBy);
        Add(cmd, "updated_at", NpgsqlDbType.TimestampTz, instance.UpdatedAt);
        AddUuid(cmd, "updated_by", instance.UpdatedBy);
        Add(cmd, "row_version", NpgsqlDbType.Integer, instance.RowVersion);

        await cmd.ExecuteNonQueryAsync(ct);
    }

    private static ProcedureInstance MapRow(System.Data.Common.DbDataReader r) => new(
        Id: r.GetGuid(0),
        TenantId: r.GetGuid(1),
        ProcedureTypeId: r.GetGuid(2),
        TrafficAgencyId: r.IsDBNull(3) ? null : r.GetGuid(3),
        ReferenceNumber: r.GetString(4),
        State: r.GetString(5),
        ConfigSnapshot: r.GetString(6),
        ConfigSchemaVersion: r.GetInt32(7),
        AssignedToUserId: r.IsDBNull(8) ? null : r.GetGuid(8),
        RadicatedAt: r.IsDBNull(9) ? null : r.GetFieldValue<DateTimeOffset>(9),
        TotalAmount: r.GetDecimal(10),
        CurrencyCode: r.GetString(11),
        CreatedAt: r.GetFieldValue<DateTimeOffset>(12),
        CreatedBy: r.GetGuid(13),
        UpdatedAt: r.GetFieldValue<DateTimeOffset>(14),
        UpdatedBy: r.GetGuid(15),
        RowVersion: r.GetInt32(16));

    private static void AddUuid(System.Data.Common.DbCommand cmd, string name, Guid value) =>
        cmd.Parameters.Add(new NpgsqlParameter(name, NpgsqlDbType.Uuid) { Value = value });

    private static void AddNullableUuid(System.Data.Common.DbCommand cmd, string name, Guid? value) =>
        cmd.Parameters.Add(new NpgsqlParameter(name, NpgsqlDbType.Uuid)
        {
            Value = value.HasValue ? (object)value.Value : DBNull.Value,
        });

    private static void Add(System.Data.Common.DbCommand cmd, string name, NpgsqlDbType type, object value) =>
        cmd.Parameters.Add(new NpgsqlParameter(name, type) { Value = value });

    private static void AddNullableTimestampTz(System.Data.Common.DbCommand cmd, string name, DateTimeOffset? value) =>
        cmd.Parameters.Add(new NpgsqlParameter(name, NpgsqlDbType.TimestampTz)
        {
            Value = value.HasValue ? (object)value.Value : DBNull.Value,
        });
}
