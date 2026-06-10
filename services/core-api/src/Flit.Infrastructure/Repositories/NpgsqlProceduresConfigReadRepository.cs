using System.Text.Json;
using Npgsql;
using NpgsqlTypes;
using Microsoft.EntityFrameworkCore;
using Flit.Infrastructure.Persistence;
using Flit.Modules.ProceduresConfig.Domain;
using Flit.Modules.ProceduresConfig.Ports;

namespace Flit.Infrastructure.Repositories;

public sealed class NpgsqlProceduresConfigReadRepository(FlitDbContext db) : IProceduresConfigReadRepository
{
    public async Task<IReadOnlyList<ProcedureTypeListItem>> ListEffectiveActiveTypesAsync(
        Guid tenantId,
        Guid? trafficAgencyId,
        CancellationToken ct = default)
    {
        var conn = db.Database.GetDbConnection();
        if (conn.State != System.Data.ConnectionState.Open)
        {
            await conn.OpenAsync(ct);
        }

        await TenantDbContext.SetTenantContextAsync(conn, tenantId, ct);

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT t.id, t.code, t.slug, t.name, f.code, f.name, t.max_steps
            FROM procedures_config.procedure_types t
            JOIN procedures_config.procedure_families f ON f.id = t.family_id
            JOIN procedures_config.procedure_type_activations a
              ON a.procedure_type_id = t.id
             AND a.tenant_id = @tenant_id
             AND a.deleted_at IS NULL
             AND a.is_active = TRUE
            WHERE t.is_active = TRUE
              AND (@traffic_agency_id IS NULL OR a.traffic_agency_id = @traffic_agency_id)
            ORDER BY f.display_order, t.display_order, t.name
            """;

        AddGuidParam(cmd, "tenant_id", tenantId);
        AddNullableGuidParam(cmd, "traffic_agency_id", trafficAgencyId);

        var list = new List<ProcedureTypeListItem>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            list.Add(new ProcedureTypeListItem(
                reader.GetGuid(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetString(3),
                reader.GetString(4),
                reader.GetString(5),
                reader.GetInt32(6)));
        }

        return list;
    }

    public async Task<ProcedureConfigurationBundle?> LoadConfigurationBundleAsync(
        Guid tenantId,
        string procedureTypeCode,
        Guid? trafficAgencyId,
        CancellationToken ct = default)
    {
        var conn = db.Database.GetDbConnection();
        if (conn.State != System.Data.ConnectionState.Open)
        {
            await conn.OpenAsync(ct);
        }

        await TenantDbContext.SetTenantContextAsync(conn, tenantId, ct);

        var typeRow = await LoadTypeRowAsync(conn, procedureTypeCode, ct);
        if (typeRow is null || !typeRow.Value.GlobalIsActive)
        {
            return null;
        }

        var tenantActivation = await LoadActivationAsync(conn, tenantId, typeRow.Value.Id, null, ct);
        if (tenantActivation is null)
        {
            return null;
        }

        ProcedureActivationLayer? otActivation = null;
        if (trafficAgencyId.HasValue)
        {
            otActivation = await LoadActivationAsync(conn, tenantId, typeRow.Value.Id, trafficAgencyId, ct);
        }

        var edges = await LoadEdgesAsync(conn, typeRow.Value.Id, ct);
        var sections = await LoadSectionsAsync(conn, typeRow.Value.Id, ct);
        var queries = await LoadQueriesAsync(conn, typeRow.Value.Id, ct);
        var documents = await LoadRequiredDocumentsAsync(conn, typeRow.Value.Id, ct);

        return new ProcedureConfigurationBundle(
            typeRow.Value.Id,
            typeRow.Value.Code,
            typeRow.Value.Slug,
            typeRow.Value.Name,
            typeRow.Value.FamilyCode,
            typeRow.Value.MaxSteps,
            typeRow.Value.GlobalIsActive,
            tenantActivation,
            otActivation,
            edges,
            sections,
            queries,
            documents);
    }

    private static async Task<(Guid Id, string Code, string Slug, string Name, string FamilyCode, int MaxSteps, bool GlobalIsActive)?>
        LoadTypeRowAsync(System.Data.Common.DbConnection conn, string code, CancellationToken ct)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT t.id, t.code, t.slug, t.name, f.code, t.max_steps, t.is_active
            FROM procedures_config.procedure_types t
            JOIN procedures_config.procedure_families f ON f.id = t.family_id
            WHERE t.code = @code
            LIMIT 1
            """;
        var p = cmd.CreateParameter();
        p.ParameterName = "code";
        p.Value = code;
        cmd.Parameters.Add(p);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
        {
            return null;
        }

        return (
            reader.GetGuid(0),
            reader.GetString(1),
            reader.GetString(2),
            reader.GetString(3),
            reader.GetString(4),
            reader.GetInt32(5),
            reader.GetBoolean(6));
    }

    private static async Task<ProcedureActivationLayer?> LoadActivationAsync(
        System.Data.Common.DbConnection conn,
        Guid tenantId,
        Guid typeId,
        Guid? trafficAgencyId,
        CancellationToken ct)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT traffic_agency_id, overrides::text
            FROM procedures_config.procedure_type_activations
            WHERE tenant_id = @tenant_id
              AND procedure_type_id = @type_id
              AND deleted_at IS NULL
              AND is_active = TRUE
              AND (
                @traffic_agency_id IS NULL
                OR traffic_agency_id = @traffic_agency_id
              )
            ORDER BY
              CASE
                WHEN @traffic_agency_id IS NOT NULL AND traffic_agency_id = @traffic_agency_id THEN 0
                ELSE 1
              END,
              traffic_agency_id NULLS FIRST
            LIMIT 1
            """;

        AddGuidParam(cmd, "tenant_id", tenantId);
        AddGuidParam(cmd, "type_id", typeId);
        AddNullableGuidParam(cmd, "traffic_agency_id", trafficAgencyId);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
        {
            return null;
        }

        Guid? agencyId = reader.IsDBNull(0) ? null : reader.GetGuid(0);
        var overrides = reader.GetString(1);
        return new ProcedureActivationLayer(agencyId, overrides);
    }

    private static async Task<IReadOnlyList<ProcedureEdgeConfig>> LoadEdgesAsync(
        System.Data.Common.DbConnection conn,
        Guid typeId,
        CancellationToken ct)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT e.code, e.name, e.edge_kind, m.is_active, m.is_required, m.display_order, m.role_label
            FROM procedures_config.procedure_type_edges m
            JOIN procedures_config.edges e ON e.id = m.edge_id
            WHERE m.procedure_type_id = @type_id
            ORDER BY m.display_order
            """;
        AddGuidParam(cmd, "type_id", typeId);

        var list = new List<ProcedureEdgeConfig>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            list.Add(new ProcedureEdgeConfig(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetBoolean(3),
                reader.GetBoolean(4),
                reader.GetInt32(5),
                reader.IsDBNull(6) ? null : reader.GetString(6)));
        }

        return list;
    }

    private static async Task<IReadOnlyList<ProcedureFormSectionConfig>> LoadSectionsAsync(
        System.Data.Common.DbConnection conn,
        Guid typeId,
        CancellationToken ct)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT s.id, s.section_key, s.title, s.display_order, s.ui_mode, ed.code
            FROM procedures_config.form_sections s
            LEFT JOIN procedures_config.edges ed ON ed.id = s.edge_id
            WHERE s.procedure_type_id = @type_id AND s.is_active = TRUE
            ORDER BY s.display_order
            """;
        AddGuidParam(cmd, "type_id", typeId);

        var sections = new List<(Guid Id, ProcedureFormSectionConfig Section)>();
        await using (var reader = await cmd.ExecuteReaderAsync(ct))
        {
            while (await reader.ReadAsync(ct))
            {
                sections.Add((
                    reader.GetGuid(0),
                    new ProcedureFormSectionConfig(
                        reader.GetString(1),
                        reader.GetString(2),
                        reader.GetInt32(3),
                        reader.GetString(4),
                        reader.IsDBNull(5) ? null : reader.GetString(5),
                        [])));
            }
        }

        if (sections.Count == 0)
        {
            return [];
        }

        var result = new List<ProcedureFormSectionConfig>();
        foreach (var (sectionId, section) in sections)
        {
            var fields = await LoadFieldsAsync(conn, sectionId, ct);
            result.Add(section with { Fields = fields });
        }

        return result;
    }

    private static async Task<IReadOnlyList<ProcedureFormFieldConfig>> LoadFieldsAsync(
        System.Data.Common.DbConnection conn,
        Guid sectionId,
        CancellationToken ct)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT field_key, data_type, label, is_required, display_order, ui_state, is_trigger,
                   validation::text, options::text
            FROM procedures_config.form_fields
            WHERE section_id = @section_id AND is_active = TRUE
            ORDER BY display_order
            """;
        AddGuidParam(cmd, "section_id", sectionId);

        var list = new List<ProcedureFormFieldConfig>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            list.Add(new ProcedureFormFieldConfig(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetBoolean(3),
                reader.GetInt32(4),
                reader.GetString(5),
                reader.GetBoolean(6),
                ParseJsonObject(reader.GetString(7)),
                ParseJsonArray(reader.GetString(8))));
        }

        return list;
    }

    private static async Task<IReadOnlyList<ProcedureQueryConfig>> LoadQueriesAsync(
        System.Data.Common.DbConnection conn,
        Guid typeId,
        CancellationToken ct)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT qc.code, ed.code, q.is_mandatory, q.is_omitible, q.person_kind_filter, q.display_order
            FROM procedures_config.procedure_type_query_configs q
            JOIN procedures_config.query_connectors qc ON qc.id = q.query_connector_id
            LEFT JOIN procedures_config.edges ed ON ed.id = q.edge_id
            WHERE q.procedure_type_id = @type_id AND q.is_active = TRUE
            ORDER BY q.display_order
            """;
        AddGuidParam(cmd, "type_id", typeId);

        var list = new List<ProcedureQueryConfig>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            list.Add(new ProcedureQueryConfig(
                reader.GetString(0),
                reader.IsDBNull(1) ? null : reader.GetString(1),
                reader.GetBoolean(2),
                reader.GetBoolean(3),
                reader.GetString(4),
                reader.GetInt32(5)));
        }

        return list;
    }

    private static async Task<IReadOnlyList<ProcedureRequiredDocumentConfig>> LoadRequiredDocumentsAsync(
        System.Data.Common.DbConnection conn,
        Guid typeId,
        CancellationToken ct)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT COALESCE(pdc.code, dt.code),
                   COALESCE(pdc.name, dt.name, pdc.code, dt.code),
                   ed.code, rd.kind, rd.is_required, rd.display_order, rd.actor_role
            FROM procedures_config.required_documents rd
            LEFT JOIN procedures_config.procedure_document_catalog pdc
              ON pdc.id = rd.procedure_document_catalog_id
            LEFT JOIN catalogs.document_types dt ON dt.id = rd.document_type_id
            LEFT JOIN procedures_config.edges ed ON ed.id = rd.edge_id
            WHERE rd.procedure_type_id = @type_id AND rd.is_active = TRUE
            ORDER BY rd.display_order
            """;
        AddGuidParam(cmd, "type_id", typeId);

        var list = new List<ProcedureRequiredDocumentConfig>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            list.Add(new ProcedureRequiredDocumentConfig(
                reader.GetString(0),
                reader.GetString(1),
                reader.IsDBNull(2) ? null : reader.GetString(2),
                reader.GetString(3),
                reader.GetBoolean(4),
                reader.GetInt32(5),
                reader.IsDBNull(6) ? null : reader.GetString(6)));
        }

        return list;
    }

    private static Dictionary<string, object?> ParseJsonObject(string json)
    {
        if (string.IsNullOrWhiteSpace(json) || json == "{}")
        {
            return new Dictionary<string, object?>();
        }

        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.EnumerateObject()
            .ToDictionary(p => p.Name, p => (object?)p.Value.ToString());
    }

    private static List<object> ParseJsonArray(string json)
    {
        if (string.IsNullOrWhiteSpace(json) || json == "[]")
        {
            return [];
        }

        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.EnumerateArray().Select(e => (object)e.ToString()!).ToList();
    }

    private static void AddGuidParam(System.Data.Common.DbCommand cmd, string name, Guid value)
    {
        cmd.Parameters.Add(new NpgsqlParameter(name, NpgsqlDbType.Uuid) { Value = value });
    }

    private static void AddNullableGuidParam(System.Data.Common.DbCommand cmd, string name, Guid? value)
    {
        cmd.Parameters.Add(new NpgsqlParameter(name, NpgsqlDbType.Uuid)
        {
            Value = value.HasValue ? value.Value : DBNull.Value,
        });
    }
}
