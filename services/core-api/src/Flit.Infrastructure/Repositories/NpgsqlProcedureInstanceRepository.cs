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

    public async Task<ProcedureInstanceSearchResult> SearchAsync(
        Guid tenantId,
        int page,
        int pageSize,
        string? state,
        string? procedureTypeCode,
        CancellationToken ct = default)
    {
        var conn = db.Database.GetDbConnection();
        if (conn.State != ConnectionState.Open)
            await conn.OpenAsync(ct);

        await TenantDbContext.SetTenantContextAsync(conn, tenantId, ct);

        var filters = new List<string> { "pi.tenant_id = @tenant_id", "pi.deleted_at IS NULL" };
        if (!string.IsNullOrWhiteSpace(state))
            filters.Add("pi.state = @state");
        if (!string.IsNullOrWhiteSpace(procedureTypeCode))
            filters.Add("pt.code = @procedure_type_code");

        var whereClause = string.Join(" AND ", filters);
        var offset = (page - 1) * pageSize;

        await using var countCmd = conn.CreateCommand();
        countCmd.CommandText = $"""
            SELECT COUNT(*)
            FROM procedures.procedure_instances pi
            INNER JOIN procedures_config.procedure_types pt ON pt.id = pi.procedure_type_id
            WHERE {whereClause}
            """;
        AddUuid(countCmd, "tenant_id", tenantId);
        if (!string.IsNullOrWhiteSpace(state))
            Add(countCmd, "state", NpgsqlDbType.Text, state.Trim());
        if (!string.IsNullOrWhiteSpace(procedureTypeCode))
            Add(countCmd, "procedure_type_code", NpgsqlDbType.Text, procedureTypeCode.Trim());

        var total = Convert.ToInt32(await countCmd.ExecuteScalarAsync(ct));

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = $"""
            SELECT pi.id, pi.tenant_id, pi.reference_number, pt.code, pi.state,
                   pi.procedure_type_id, pi.traffic_agency_id, pi.radicated_at,
                   pi.created_at, pi.created_by
            FROM procedures.procedure_instances pi
            INNER JOIN procedures_config.procedure_types pt ON pt.id = pi.procedure_type_id
            WHERE {whereClause}
            ORDER BY pi.created_at DESC
            LIMIT @page_size OFFSET @offset
            """;
        AddUuid(cmd, "tenant_id", tenantId);
        if (!string.IsNullOrWhiteSpace(state))
            Add(cmd, "state", NpgsqlDbType.Text, state.Trim());
        if (!string.IsNullOrWhiteSpace(procedureTypeCode))
            Add(cmd, "procedure_type_code", NpgsqlDbType.Text, procedureTypeCode.Trim());
        Add(cmd, "page_size", NpgsqlDbType.Integer, pageSize);
        Add(cmd, "offset", NpgsqlDbType.Integer, offset);

        var items = new List<ProcedureInstanceSearchRow>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var reference = reader.GetString(2);
            items.Add(new ProcedureInstanceSearchRow(
                reader.GetGuid(0),
                reader.GetGuid(1),
                reference,
                reference,
                reader.GetString(3),
                reader.GetString(4),
                reader.GetGuid(5),
                reader.IsDBNull(6) ? null : reader.GetGuid(6),
                reader.IsDBNull(7) ? null : reader.GetFieldValue<DateTimeOffset>(7),
                reader.GetFieldValue<DateTimeOffset>(8),
                reader.GetGuid(9)));
        }

        return new ProcedureInstanceSearchResult(items, total);
    }

    public async Task<int> GetNextSequenceAsync(
        Guid tenantId,
        Guid procedureTypeId,
        Guid? trafficAgencyId,
        CancellationToken ct = default)
    {
        var conn = db.Database.GetDbConnection();
        if (conn.State != ConnectionState.Open)
            await conn.OpenAsync(ct);

        await TenantDbContext.SetTenantContextAsync(conn, tenantId, ct);

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT COUNT(*) + 1
            FROM procedures.procedure_instances
            WHERE tenant_id = @tenant_id
              AND procedure_type_id = @procedure_type_id
              AND (
                (traffic_agency_id IS NULL AND @traffic_agency_id IS NULL)
                OR traffic_agency_id = @traffic_agency_id
              )
              AND deleted_at IS NULL
            """;
        AddUuid(cmd, "tenant_id", tenantId);
        AddUuid(cmd, "procedure_type_id", procedureTypeId);
        AddNullableUuid(cmd, "traffic_agency_id", trafficAgencyId);

        var result = await cmd.ExecuteScalarAsync(ct);
        return Convert.ToInt32(result);
    }

    public async Task<ProcedureReferenceContext> ResolveReferenceContextAsync(
        Guid tenantId,
        Guid? trafficAgencyId,
        string procedureTypeCode,
        CancellationToken ct = default)
    {
        var conn = db.Database.GetDbConnection();
        if (conn.State != ConnectionState.Open)
            await conn.OpenAsync(ct);

        await TenantDbContext.SetTenantContextAsync(conn, tenantId, ct);

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT
              COALESCE(
                NULLIF(t.settings->>'reference_code', ''),
                UPPER(LEFT(REPLACE(c.commercial_name, ' ', ''), 2)),
                UPPER(LEFT(t.slug, 2))
              ) AS tenant_code,
              COALESCE(
                UPPER(REPLACE(REPLACE(ta.code, 'OT-', ''), '-', '')),
                'GEN'
              ) AS ot_code
            FROM identity.tenants t
            LEFT JOIN companies.companies c ON c.tenant_id = t.id AND c.deleted_at IS NULL
            LEFT JOIN ot.traffic_agencies ta ON ta.id = @traffic_agency_id AND ta.deleted_at IS NULL
            WHERE t.id = @tenant_id
            LIMIT 1
            """;
        AddUuid(cmd, "tenant_id", tenantId);
        AddNullableUuid(cmd, "traffic_agency_id", trafficAgencyId);

        try
        {
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            if (await reader.ReadAsync(ct))
            {
                var tenantCode = reader.GetString(0);
                var otCode = reader.IsDBNull(1) ? "GEN" : reader.GetString(1);
                if (otCode.Length > 8)
                    otCode = otCode[..8];
                return new ProcedureReferenceContext(
                    ProcedureReferenceFormatter.AbbreviateProcedureTypeCode(procedureTypeCode),
                    tenantCode.Length > 8 ? tenantCode[..8] : tenantCode,
                    otCode);
            }
        }
        catch (PostgresException ex) when (ex.SqlState is "42P01")
        {
        }

        return new ProcedureReferenceContext(
            ProcedureReferenceFormatter.AbbreviateProcedureTypeCode(procedureTypeCode),
            tenantId.ToString("N")[..2].ToUpperInvariant(),
            trafficAgencyId?.ToString("N")[..3].ToUpperInvariant() ?? "GEN");
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
