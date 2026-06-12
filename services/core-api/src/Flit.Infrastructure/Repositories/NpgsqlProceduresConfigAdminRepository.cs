using System.Text.Json;
using Npgsql;
using NpgsqlTypes;
using Microsoft.EntityFrameworkCore;
using Flit.Infrastructure.Persistence;
using Flit.Modules.ProceduresConfig.Domain;
using Flit.Modules.ProceduresConfig.Ports;

namespace Flit.Infrastructure.Repositories;

/// <summary>Administración de matriz de trámites persistida en PostgreSQL (#9409).</summary>
public sealed class NpgsqlProceduresConfigAdminRepository(FlitDbContext db) : IProceduresConfigAdminRepository
{
    private static readonly Guid SystemUserId = Guid.Parse("00000000-0000-7000-8000-000000000001");

    public async Task<IReadOnlyList<AdminProcedureTypeSummary>> ListTypesForAdminAsync(
        Guid tenantId,
        Guid? trafficAgencyId,
        CancellationToken ct = default)
    {
        var conn = await OpenWithConfigAdminContextAsync(tenantId, ct);

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT
              t.id,
              t.code,
              t.name,
              f.code,
              t.is_active,
              EXISTS (
                SELECT 1
                FROM procedures_config.procedure_type_activations a
                WHERE a.procedure_type_id = t.id
                  AND a.tenant_id = @tenant_id
                  AND a.deleted_at IS NULL
                  AND a.is_active = TRUE
                  AND (@traffic_agency_id IS NULL OR a.traffic_agency_id = @traffic_agency_id)
              ) AS tenant_activation_active,
              (
                SELECT COUNT(*)::int
                FROM procedures_config.procedure_type_edges m
                WHERE m.procedure_type_id = t.id AND m.is_active = TRUE
              ) AS active_edge_count,
              t.max_steps
            FROM procedures_config.procedure_types t
            JOIN procedures_config.procedure_families f ON f.id = t.family_id
            ORDER BY f.display_order, t.display_order, t.name
            """;

        AddGuidParam(cmd, "tenant_id", tenantId);
        AddNullableGuidParam(cmd, "traffic_agency_id", trafficAgencyId);

        var list = new List<AdminProcedureTypeSummary>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            list.Add(new AdminProcedureTypeSummary(
                reader.GetGuid(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetString(3),
                reader.GetBoolean(4),
                reader.GetBoolean(5),
                reader.GetInt32(6),
                reader.GetInt32(7)));
        }

        return list;
    }

    public async Task<AdminProcedureMatrixView?> GetMatrixAsync(
        Guid tenantId,
        string procedureTypeCode,
        Guid? trafficAgencyId,
        CancellationToken ct = default)
    {
        var conn = await OpenWithConfigAdminContextAsync(tenantId, ct);
        var typeRow = await LoadTypeRowAdminAsync(conn, procedureTypeCode, ct);
        if (typeRow is null)
        {
            return null;
        }

        var tenantActivationActive = await LoadTenantActivationIsActiveAsync(
            conn,
            tenantId,
            typeRow.Id,
            trafficAgencyId,
            ct);
        var edges = await LoadEdgesAdminAsync(conn, typeRow.Id, ct);
        var sections = await LoadSectionsAdminAsync(conn, typeRow.Id, ct);
        var documents = await LoadRequiredDocumentsAdminAsync(conn, typeRow.Id, ct);

        return new AdminProcedureMatrixView(
            typeRow.Id,
            typeRow.Code,
            typeRow.Name,
            typeRow.FamilyCode,
            typeRow.MaxSteps,
            typeRow.GlobalIsActive,
            tenantActivationActive,
            edges,
            sections,
            documents);
    }

    public async Task<bool> SetTenantActivationAsync(
        Guid tenantId,
        string procedureTypeCode,
        Guid? trafficAgencyId,
        bool isActive,
        CancellationToken ct = default)
    {
        var conn = await OpenWithConfigAdminContextAsync(tenantId, ct);
        var typeId = await LoadTypeIdByCodeAsync(conn, procedureTypeCode, ct);
        if (typeId is null)
        {
            return false;
        }

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            UPDATE procedures_config.procedure_type_activations
            SET is_active = @is_active,
                deleted_at = NULL,
                updated_at = now(),
                updated_by = @user
            WHERE tenant_id = @tenant_id
              AND procedure_type_id = @type_id
              AND deleted_at IS NULL
              AND (
                (@ot_id IS NULL AND traffic_agency_id IS NULL)
                OR traffic_agency_id = @ot_id
              )
            """;

        AddGuidParam(cmd, "tenant_id", tenantId);
        AddGuidParam(cmd, "type_id", typeId.Value);
        AddNullableGuidParam(cmd, "ot_id", trafficAgencyId);
        cmd.Parameters.Add(new NpgsqlParameter("is_active", isActive));
        AddGuidParam(cmd, "user", SystemUserId);

        var updated = await cmd.ExecuteNonQueryAsync(ct);
        if (updated > 0)
        {
            return true;
        }

        await using var insertCmd = conn.CreateCommand();
        insertCmd.CommandText = """
            INSERT INTO procedures_config.procedure_type_activations
              (tenant_id, procedure_type_id, traffic_agency_id, is_active, overrides, created_by, updated_by)
            VALUES
              (@tenant_id, @type_id, @ot_id, @is_active, '{}'::jsonb, @user, @user)
            """;

        AddGuidParam(insertCmd, "tenant_id", tenantId);
        AddGuidParam(insertCmd, "type_id", typeId.Value);
        AddNullableGuidParam(insertCmd, "ot_id", trafficAgencyId);
        insertCmd.Parameters.Add(new NpgsqlParameter("is_active", isActive));
        AddGuidParam(insertCmd, "user", SystemUserId);

        var inserted = await insertCmd.ExecuteNonQueryAsync(ct);
        return inserted > 0;
    }

    public async Task<bool> SetGlobalTypeActiveAsync(
        Guid tenantId,
        string procedureTypeCode,
        bool isActive,
        CancellationToken ct = default)
    {
        var conn = await OpenWithConfigAdminContextAsync(tenantId, ct);

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            UPDATE procedures_config.procedure_types
            SET is_active = @is_active,
                updated_at = now(),
                updated_by = @updated_by
            WHERE code = @code
            """;

        cmd.Parameters.Add(new NpgsqlParameter("is_active", isActive));
        AddGuidParam(cmd, "updated_by", SystemUserId);
        cmd.Parameters.Add(new NpgsqlParameter("code", procedureTypeCode));

        var rows = await cmd.ExecuteNonQueryAsync(ct);
        return rows > 0;
    }

    public async Task<bool> UpdateProcedureTypeAsync(
        Guid tenantId,
        string procedureTypeCode,
        string? name,
        int? maxSteps,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(name) && maxSteps is null)
        {
            return false;
        }

        if (maxSteps is < 1 or > 4)
        {
            return false;
        }

        var conn = await OpenWithConfigAdminContextAsync(tenantId, ct);

        await using var cmd = conn.CreateCommand();
        var sets = new List<string> { "updated_at = now()", "updated_by = @updated_by" };
        if (!string.IsNullOrWhiteSpace(name))
        {
            sets.Add("name = @name");
        }

        if (maxSteps.HasValue)
        {
            sets.Add("max_steps = @max_steps");
        }

        cmd.CommandText = $"""
            UPDATE procedures_config.procedure_types
            SET {string.Join(", ", sets)}
            WHERE code = @code
            """;

        AddGuidParam(cmd, "updated_by", SystemUserId);
        cmd.Parameters.Add(new NpgsqlParameter("code", procedureTypeCode));
        if (!string.IsNullOrWhiteSpace(name))
        {
            cmd.Parameters.Add(new NpgsqlParameter("name", name.Trim()));
        }

        if (maxSteps.HasValue)
        {
            cmd.Parameters.Add(new NpgsqlParameter("max_steps", maxSteps.Value));
        }

        var rows = await cmd.ExecuteNonQueryAsync(ct);
        return rows > 0;
    }

    public async Task<bool> SetEdgeActiveAsync(
        Guid tenantId,
        string procedureTypeCode,
        string edgeCode,
        bool isActive,
        CancellationToken ct = default)
    {
        var conn = await OpenWithConfigAdminContextAsync(tenantId, ct);
        await using var tx = await conn.BeginTransactionAsync(ct);

        try
        {
            await using var cmd = conn.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = """
                UPDATE procedures_config.procedure_type_edges m
                SET is_active = @is_active,
                    updated_at = now(),
                    updated_by = @updated_by
                FROM procedures_config.procedure_types t,
                     procedures_config.edges e
                WHERE m.procedure_type_id = t.id
                  AND t.code = @type_code
                  AND m.edge_id = e.id
                  AND e.code = @edge_code
                """;

            AddGuidParam(cmd, "updated_by", SystemUserId);
            var pActive = cmd.CreateParameter();
            pActive.ParameterName = "is_active";
            pActive.Value = isActive;
            cmd.Parameters.Add(pActive);

            var pType = cmd.CreateParameter();
            pType.ParameterName = "type_code";
            pType.Value = procedureTypeCode;
            cmd.Parameters.Add(pType);

            var pEdge = cmd.CreateParameter();
            pEdge.ParameterName = "edge_code";
            pEdge.Value = edgeCode;
            cmd.Parameters.Add(pEdge);

            var rows = await cmd.ExecuteNonQueryAsync(ct);
            if (rows == 0)
            {
                await tx.RollbackAsync(ct);
                return false;
            }

            if (isActive && edgeCode != "documentos")
            {
                var matrixRow = await LoadMatrixEdgeRowAsync(conn, tx, procedureTypeCode, edgeCode, ct);
                if (matrixRow is not null)
                {
                    await InsertDefaultSectionAsync(
                        conn,
                        tx,
                        matrixRow.Value.TypeId,
                        matrixRow.Value.EdgeId,
                        new CreateProcedureTypeEdgeInput(
                            edgeCode,
                            true,
                            matrixRow.Value.IsRequired,
                            matrixRow.Value.DisplayOrder,
                            matrixRow.Value.RoleLabel),
                        ct);
                }
            }

            await SyncMaxStepsFromActiveMatrixAsync(conn, tx, procedureTypeCode, ct);

            await tx.CommitAsync(ct);
            return true;
        }
        catch
        {
            await tx.RollbackAsync(ct);
            throw;
        }
    }

    private static async Task<(Guid TypeId, Guid EdgeId, bool IsRequired, int DisplayOrder, string? RoleLabel)?>
        LoadMatrixEdgeRowAsync(
            System.Data.Common.DbConnection conn,
            System.Data.Common.DbTransaction tx,
            string procedureTypeCode,
            string edgeCode,
            CancellationToken ct)
    {
        await using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = """
            SELECT t.id, e.id, m.is_required, m.display_order, m.role_label
            FROM procedures_config.procedure_type_edges m
            JOIN procedures_config.procedure_types t ON t.id = m.procedure_type_id
            JOIN procedures_config.edges e ON e.id = m.edge_id
            WHERE t.code = @type_code AND e.code = @edge_code
            LIMIT 1
            """;
        cmd.Parameters.Add(new NpgsqlParameter("type_code", procedureTypeCode));
        cmd.Parameters.Add(new NpgsqlParameter("edge_code", edgeCode));

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
        {
            return null;
        }

        return (
            reader.GetGuid(0),
            reader.GetGuid(1),
            reader.GetBoolean(2),
            reader.GetInt32(3),
            reader.IsDBNull(4) ? null : reader.GetString(4));
    }

    private static async Task SyncMaxStepsFromActiveMatrixAsync(
        System.Data.Common.DbConnection conn,
        System.Data.Common.DbTransaction tx,
        string procedureTypeCode,
        CancellationToken ct)
    {
        await using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = """
            UPDATE procedures_config.procedure_types t
            SET max_steps = LEAST(4, GREATEST(1, COALESCE((
                SELECT COUNT(*)::int
                FROM procedures_config.procedure_type_edges m
                WHERE m.procedure_type_id = t.id
                  AND m.is_active = TRUE
            ), 1))),
                updated_at = now(),
                updated_by = @updated_by
            WHERE t.code = @type_code
            """;
        cmd.Parameters.Add(new NpgsqlParameter("type_code", procedureTypeCode));
        cmd.Parameters.Add(new NpgsqlParameter("updated_by", SystemUserId));
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<IReadOnlyList<AdminCatalogFamily>> ListCatalogFamiliesAsync(CancellationToken ct = default)
    {
        var conn = db.Database.GetDbConnection();
        if (conn.State != System.Data.ConnectionState.Open)
        {
            await conn.OpenAsync(ct);
        }

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT id, code, name, display_order
            FROM procedures_config.procedure_families
            WHERE is_active = TRUE
            ORDER BY display_order, name
            """;

        var list = new List<AdminCatalogFamily>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            list.Add(new AdminCatalogFamily(
                reader.GetGuid(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetInt32(3)));
        }

        return list;
    }

    public async Task<(AdminCatalogFamily? Ok, string? Error)> CreateCatalogFamilyAsync(
        CreateCatalogFamilyCommand command,
        CancellationToken ct = default)
    {
        var code = command.Code.Trim().ToUpperInvariant();
        var name = command.Name.Trim();

        if (string.IsNullOrEmpty(code) || string.IsNullOrEmpty(name))
        {
            return (null, "Código y nombre son obligatorios.");
        }

        var conn = db.Database.GetDbConnection();
        if (conn.State != System.Data.ConnectionState.Open)
        {
            await conn.OpenAsync(ct);
        }

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO procedures_config.procedure_families
              (code, name, display_order, is_active, created_by, updated_by)
            VALUES
              (@code, @name, @display_order, TRUE, @user, @user)
            RETURNING id, code, name, display_order
            """;

        cmd.Parameters.Add(new NpgsqlParameter("code", code));
        cmd.Parameters.Add(new NpgsqlParameter("name", name));
        cmd.Parameters.Add(new NpgsqlParameter("display_order", command.DisplayOrder));
        AddGuidParam(cmd, "user", SystemUserId);

        try
        {
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            if (!await reader.ReadAsync(ct))
            {
                return (null, "No se pudo crear la familia.");
            }

            return (new AdminCatalogFamily(
                reader.GetGuid(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetInt32(3)), null);
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            return (null, $"Ya existe la familia '{code}'.");
        }
    }

    public async Task<(bool Ok, DeleteCatalogFamilyError? Error)> DeleteCatalogFamilyAsync(
        string familyCode,
        CancellationToken ct = default)
    {
        var code = familyCode.Trim().ToUpperInvariant();
        if (string.IsNullOrEmpty(code))
        {
            return (false, new DeleteCatalogFamilyError(DeleteCatalogFamilyErrorKind.NotFound, "Código de familia inválido."));
        }

        var conn = db.Database.GetDbConnection();
        if (conn.State != System.Data.ConnectionState.Open)
        {
            await conn.OpenAsync(ct);
        }

        await using var tx = await conn.BeginTransactionAsync(ct);
        try
        {
            Guid? familyId = null;
            await using (var loadCmd = conn.CreateCommand())
            {
                loadCmd.Transaction = tx;
                loadCmd.CommandText = """
                    SELECT id
                    FROM procedures_config.procedure_families
                    WHERE code = @code
                    LIMIT 1
                    """;
                loadCmd.Parameters.Add(new NpgsqlParameter("code", code));
                var result = await loadCmd.ExecuteScalarAsync(ct);
                if (result is not Guid g)
                {
                    await tx.RollbackAsync(ct);
                    return (false, new DeleteCatalogFamilyError(
                        DeleteCatalogFamilyErrorKind.NotFound,
                        $"Familia '{code}' no encontrada."));
                }

                familyId = g;
            }

            await using (var typesCmd = conn.CreateCommand())
            {
                typesCmd.Transaction = tx;
                typesCmd.CommandText = """
                    SELECT 1
                    FROM procedures_config.procedure_types
                    WHERE family_id = @family_id
                    LIMIT 1
                    """;
                typesCmd.Parameters.Add(new NpgsqlParameter("family_id", familyId.Value));
                if (await typesCmd.ExecuteScalarAsync(ct) is not null)
                {
                    await tx.RollbackAsync(ct);
                    return (false, new DeleteCatalogFamilyError(
                        DeleteCatalogFamilyErrorKind.FamilyInUse,
                        "FAMILY_IN_USE: existen tipos de trámite asociados a la familia."));
                }
            }

            await using (var instancesCmd = conn.CreateCommand())
            {
                instancesCmd.Transaction = tx;
                instancesCmd.CommandText = """
                    SELECT 1
                    FROM procedures.procedure_instances pi
                    INNER JOIN procedures_config.procedure_types pt ON pt.id = pi.procedure_type_id
                    WHERE pt.family_id = @family_id
                      AND pi.deleted_at IS NULL
                    LIMIT 1
                    """;
                instancesCmd.Parameters.Add(new NpgsqlParameter("family_id", familyId.Value));
                if (await instancesCmd.ExecuteScalarAsync(ct) is not null)
                {
                    await tx.RollbackAsync(ct);
                    return (false, new DeleteCatalogFamilyError(
                        DeleteCatalogFamilyErrorKind.FamilyInUse,
                        "FAMILY_IN_USE: existen instancias activas de trámites asociadas a la familia."));
                }
            }

            await using (var deleteCmd = conn.CreateCommand())
            {
                deleteCmd.Transaction = tx;
                deleteCmd.CommandText = """
                    DELETE FROM procedures_config.procedure_families
                    WHERE id = @family_id
                    """;
                deleteCmd.Parameters.Add(new NpgsqlParameter("family_id", familyId.Value));
                var deleted = await deleteCmd.ExecuteNonQueryAsync(ct);
                if (deleted == 0)
                {
                    await tx.RollbackAsync(ct);
                    return (false, new DeleteCatalogFamilyError(
                        DeleteCatalogFamilyErrorKind.NotFound,
                        $"Familia '{code}' no encontrada."));
                }
            }

            await tx.CommitAsync(ct);
            return (true, null);
        }
        catch
        {
            await tx.RollbackAsync(ct);
            throw;
        }
    }

    public async Task<IReadOnlyList<AdminCatalogEdge>> ListCatalogEdgesAsync(CancellationToken ct = default)
    {
        var conn = db.Database.GetDbConnection();
        if (conn.State != System.Data.ConnectionState.Open)
        {
            await conn.OpenAsync(ct);
        }

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT code, name, edge_kind, display_order
            FROM procedures_config.edges
            WHERE is_active = TRUE
            ORDER BY display_order
            """;

        var list = new List<AdminCatalogEdge>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            list.Add(new AdminCatalogEdge(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetInt32(3)));
        }

        return list;
    }

    public async Task<(CreateProcedureTypeResult? Ok, CreateProcedureTypeError? Error)> CreateProcedureTypeAsync(
        CreateProcedureTypeCommand command,
        CancellationToken ct = default)
    {
        var code = command.Code.Trim().ToUpperInvariant();
        var slug = string.IsNullOrWhiteSpace(command.Slug)
            ? code.ToLowerInvariant().Replace('_', '-')
            : command.Slug.Trim().ToLowerInvariant();
        var name = command.Name.Trim();

        if (string.IsNullOrEmpty(code) || string.IsNullOrEmpty(name))
        {
            return (null, new CreateProcedureTypeError(CreateProcedureTypeErrorKind.Validation, "Código y nombre son obligatorios."));
        }

        var activeMatrixEdges = command.Edges.Where(e => e.IsActive).ToList();
        if (activeMatrixEdges.Count == 0)
        {
            return (null, new CreateProcedureTypeError(CreateProcedureTypeErrorKind.Validation, "Debe activar al menos una arista en la matriz."));
        }

        var pipelineValidation = ProcedurePipelineValidator.ValidateMinActiveDataEdges(
            command.Edges.Select(e => (e.EdgeCode, e.IsActive)));
        if (!pipelineValidation.IsValid)
        {
            return (null, new CreateProcedureTypeError(CreateProcedureTypeErrorKind.Validation, pipelineValidation.ErrorMessage!));
        }

        var activeCaptureEdges = activeMatrixEdges
            .Where(e => e.EdgeCode != ProcedurePipelineValidator.DocumentsEdgeCode)
            .ToList();

        var computedMaxSteps = Math.Clamp(activeMatrixEdges.Count, 1, 4);

        var conn = await OpenWithConfigAdminContextAsync(command.TenantId, ct);
        await using var tx = await conn.BeginTransactionAsync(ct);

        try
        {
            var familyId = await LoadFamilyIdAsync(conn, tx, command.FamilyCode, ct);
            if (familyId is null)
            {
                await tx.RollbackAsync(ct);
                return (null, new CreateProcedureTypeError(CreateProcedureTypeErrorKind.NotFound, $"Familia '{command.FamilyCode}' no existe."));
            }

            if (await TypeCodeExistsAsync(conn, tx, code, ct))
            {
                await tx.RollbackAsync(ct);
                return (null, new CreateProcedureTypeError(CreateProcedureTypeErrorKind.Conflict, $"Ya existe el tipo '{code}'."));
            }

            var typeId = await InsertProcedureTypeAsync(conn, tx, familyId.Value, code, slug, name, computedMaxSteps, ct);

            foreach (var edge in command.Edges.OrderBy(e => e.DisplayOrder))
            {
                var edgeId = await LoadEdgeIdAsync(conn, tx, edge.EdgeCode, ct);
                if (edgeId is null)
                {
                    await tx.RollbackAsync(ct);
                    return (null, new CreateProcedureTypeError(CreateProcedureTypeErrorKind.NotFound, $"Arista '{edge.EdgeCode}' no existe en catálogo."));
                }

                await InsertMatrixEdgeAsync(conn, tx, typeId, edgeId.Value, edge, ct);
                if (edge.IsActive)
                {
                    await InsertDefaultSectionAsync(conn, tx, typeId, edgeId.Value, edge, ct);
                }
            }

            var docEdgeId = await LoadGlobalEdgeIdAsync(conn, tx, "documentos", ct);
            if (docEdgeId is not null)
            {
                await InsertMatrixEdgeAsync(conn, tx, typeId, docEdgeId.Value, new CreateProcedureTypeEdgeInput(
                    "documentos", true, false, activeCaptureEdges.Max(e => e.DisplayOrder) + 1, "Documentos"), ct);
                foreach (var edge in activeCaptureEdges.Where(e => e.EdgeCode is "propietario" or "comprador" or "locatario"))
                {
                    await InsertDefaultRequiredDocumentAsync(conn, tx, typeId, docEdgeId.Value, edge.EdgeCode, edge.DisplayOrder, ct);
                }
            }

            await InsertTenantActivationAsync(conn, tx, command.TenantId, typeId, command.TrafficAgencyId, ct);

            await SyncMaxStepsFromActiveMatrixAsync(conn, tx, code, ct);

            await tx.CommitAsync(ct);
            return (new CreateProcedureTypeResult(typeId, code, slug, name), null);
        }
        catch
        {
            await tx.RollbackAsync(ct);
            throw;
        }
    }

    private sealed record TypeRowAdmin(
        Guid Id,
        string Code,
        string Name,
        string FamilyCode,
        int MaxSteps,
        bool GlobalIsActive);

    private static async Task<TypeRowAdmin?> LoadTypeRowAdminAsync(
        System.Data.Common.DbConnection conn,
        string code,
        CancellationToken ct)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT t.id, t.code, t.name, f.code, t.max_steps, t.is_active
            FROM procedures_config.procedure_types t
            JOIN procedures_config.procedure_families f ON f.id = t.family_id
            WHERE t.code = @code
            LIMIT 1
            """;
        cmd.Parameters.Add(new NpgsqlParameter("code", code));
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
        {
            return null;
        }

        return new TypeRowAdmin(
            reader.GetGuid(0),
            reader.GetString(1),
            reader.GetString(2),
            reader.GetString(3),
            reader.GetInt32(4),
            reader.GetBoolean(5));
    }

    private static async Task<Guid?> LoadTypeIdByCodeAsync(
        System.Data.Common.DbConnection conn,
        string code,
        CancellationToken ct)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT id FROM procedures_config.procedure_types WHERE code = @code LIMIT 1";
        cmd.Parameters.Add(new NpgsqlParameter("code", code));
        var result = await cmd.ExecuteScalarAsync(ct);
        return result is Guid g ? g : null;
    }

    private static async Task<bool> LoadTenantActivationIsActiveAsync(
        System.Data.Common.DbConnection conn,
        Guid tenantId,
        Guid typeId,
        Guid? trafficAgencyId,
        CancellationToken ct)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT is_active
            FROM procedures_config.procedure_type_activations
            WHERE tenant_id = @tenant_id
              AND procedure_type_id = @type_id
              AND deleted_at IS NULL
              AND (
                (@traffic_agency_id IS NULL AND traffic_agency_id IS NULL)
                OR traffic_agency_id = @traffic_agency_id
              )
            ORDER BY
              CASE
                WHEN @traffic_agency_id IS NOT NULL AND traffic_agency_id = @traffic_agency_id THEN 0
                ELSE 1
              END
            LIMIT 1
            """;
        AddGuidParam(cmd, "tenant_id", tenantId);
        AddGuidParam(cmd, "type_id", typeId);
        AddNullableGuidParam(cmd, "traffic_agency_id", trafficAgencyId);
        var result = await cmd.ExecuteScalarAsync(ct);
        return result is bool b && b;
    }

    private static async Task<IReadOnlyList<ProcedureEdgeConfig>> LoadEdgesAdminAsync(
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

    private static async Task<IReadOnlyList<ProcedureFormSectionConfig>> LoadSectionsAdminAsync(
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

        var result = new List<ProcedureFormSectionConfig>();
        foreach (var (sectionId, section) in sections)
        {
            var fields = await LoadFieldsAdminAsync(conn, sectionId, ct);
            result.Add(section with { Fields = fields });
        }

        return result;
    }

    private static async Task<IReadOnlyList<ProcedureFormFieldConfig>> LoadFieldsAdminAsync(
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

    private static async Task<IReadOnlyList<AdminRequiredDocumentItem>> LoadRequiredDocumentsAdminAsync(
        System.Data.Common.DbConnection conn,
        Guid typeId,
        CancellationToken ct)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT rd.id,
              COALESCE(pdc.code, dt.code),
              COALESCE(pdc.name, dt.name),
              ed.code,
              rd.kind,
              rd.is_required,
              rd.display_order,
              rd.actor_role,
              rd.is_active
            FROM procedures_config.required_documents rd
            LEFT JOIN procedures_config.procedure_document_catalog pdc
              ON pdc.id = rd.procedure_document_catalog_id
            LEFT JOIN catalogs.document_types dt ON dt.id = rd.document_type_id
            LEFT JOIN procedures_config.edges ed ON ed.id = rd.edge_id
            WHERE rd.procedure_type_id = @type_id
            ORDER BY rd.display_order, COALESCE(pdc.code, dt.code)
            """;
        AddGuidParam(cmd, "type_id", typeId);
        var list = new List<AdminRequiredDocumentItem>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            list.Add(new AdminRequiredDocumentItem(
                reader.GetGuid(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.IsDBNull(3) ? null : reader.GetString(3),
                reader.GetString(4),
                reader.GetBoolean(5),
                reader.GetInt32(6),
                reader.IsDBNull(7) ? null : reader.GetString(7),
                reader.GetBoolean(8)));
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

    private static async Task<Guid?> LoadFamilyIdAsync(
        System.Data.Common.DbConnection conn,
        System.Data.Common.DbTransaction tx,
        string familyCode,
        CancellationToken ct)
    {
        await using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = "SELECT id FROM procedures_config.procedure_families WHERE code = @code LIMIT 1";
        var p = new NpgsqlParameter("code", familyCode);
        cmd.Parameters.Add(p);
        var result = await cmd.ExecuteScalarAsync(ct);
        return result is Guid g ? g : null;
    }

    private static async Task<bool> TypeCodeExistsAsync(
        System.Data.Common.DbConnection conn,
        System.Data.Common.DbTransaction tx,
        string code,
        CancellationToken ct)
    {
        await using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = "SELECT 1 FROM procedures_config.procedure_types WHERE code = @code LIMIT 1";
        cmd.Parameters.Add(new NpgsqlParameter("code", code));
        return await cmd.ExecuteScalarAsync(ct) is not null;
    }

    private static async Task<Guid> InsertProcedureTypeAsync(
        System.Data.Common.DbConnection conn,
        System.Data.Common.DbTransaction tx,
        Guid familyId,
        string code,
        string slug,
        string name,
        int maxSteps,
        CancellationToken ct)
    {
        await using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = """
            INSERT INTO procedures_config.procedure_types
              (family_id, code, slug, name, display_order, max_steps, is_active, created_by, updated_by)
            VALUES
              (@family_id, @code, @slug, @name, 99, @max_steps, TRUE, @user, @user)
            RETURNING id
            """;
        cmd.Parameters.Add(new NpgsqlParameter("family_id", familyId));
        cmd.Parameters.Add(new NpgsqlParameter("code", code));
        cmd.Parameters.Add(new NpgsqlParameter("slug", slug));
        cmd.Parameters.Add(new NpgsqlParameter("name", name));
        cmd.Parameters.Add(new NpgsqlParameter("max_steps", maxSteps));
        cmd.Parameters.Add(new NpgsqlParameter("user", SystemUserId));
        return (Guid)(await cmd.ExecuteScalarAsync(ct))!;
    }

    private static async Task<Guid?> LoadEdgeIdAsync(
        System.Data.Common.DbConnection conn,
        System.Data.Common.DbTransaction tx,
        string edgeCode,
        CancellationToken ct)
    {
        await using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = "SELECT id FROM procedures_config.edges WHERE code = @code LIMIT 1";
        cmd.Parameters.Add(new NpgsqlParameter("code", edgeCode));
        var result = await cmd.ExecuteScalarAsync(ct);
        return result is Guid g ? g : null;
    }

    private static async Task InsertMatrixEdgeAsync(
        System.Data.Common.DbConnection conn,
        System.Data.Common.DbTransaction tx,
        Guid typeId,
        Guid edgeId,
        CreateProcedureTypeEdgeInput edge,
        CancellationToken ct)
    {
        await using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = """
            INSERT INTO procedures_config.procedure_type_edges
              (procedure_type_id, edge_id, is_active, is_required, display_order, role_label, created_by, updated_by)
            VALUES
              (@type_id, @edge_id, @is_active, @is_required, @display_order, @role_label, @user, @user)
            ON CONFLICT (procedure_type_id, edge_id) DO UPDATE SET
              is_active = EXCLUDED.is_active,
              is_required = EXCLUDED.is_required,
              display_order = EXCLUDED.display_order,
              role_label = EXCLUDED.role_label,
              updated_by = EXCLUDED.updated_by,
              updated_at = now()
            """;
        cmd.Parameters.Add(new NpgsqlParameter("type_id", typeId));
        cmd.Parameters.Add(new NpgsqlParameter("edge_id", edgeId));
        cmd.Parameters.Add(new NpgsqlParameter("is_active", edge.IsActive));
        cmd.Parameters.Add(new NpgsqlParameter("is_required", edge.IsRequired));
        cmd.Parameters.Add(new NpgsqlParameter("display_order", edge.DisplayOrder));
        cmd.Parameters.Add(new NpgsqlParameter("role_label", (object?)edge.RoleLabel ?? DBNull.Value));
        cmd.Parameters.Add(new NpgsqlParameter("user", SystemUserId));
        await cmd.ExecuteNonQueryAsync(ct);
    }

    private static async Task InsertDefaultSectionAsync(
        System.Data.Common.DbConnection conn,
        System.Data.Common.DbTransaction tx,
        Guid typeId,
        Guid edgeId,
        CreateProcedureTypeEdgeInput edge,
        CancellationToken ct)
    {
        var sectionKey = $"{edge.EdgeCode}_datos";
        var title = edge.RoleLabel ?? edge.EdgeCode;
        var displayOrder = edge.DisplayOrder;

        await using var secCmd = conn.CreateCommand();
        secCmd.Transaction = tx;
        secCmd.CommandText = """
            INSERT INTO procedures_config.form_sections
              (procedure_type_id, edge_id, section_key, title, display_order, ui_mode, is_active, created_by, updated_by)
            VALUES
              (@type_id, @edge_id, @section_key, @title, @display_order, 'interactive', TRUE, @user, @user)
            ON CONFLICT (procedure_type_id, section_key) DO NOTHING
            RETURNING id
            """;
        secCmd.Parameters.Add(new NpgsqlParameter("type_id", typeId));
        secCmd.Parameters.Add(new NpgsqlParameter("edge_id", edgeId));
        secCmd.Parameters.Add(new NpgsqlParameter("section_key", sectionKey));
        secCmd.Parameters.Add(new NpgsqlParameter("title", title));
        secCmd.Parameters.Add(new NpgsqlParameter("display_order", displayOrder));
        secCmd.Parameters.Add(new NpgsqlParameter("user", SystemUserId));

        var sectionIdObj = await secCmd.ExecuteScalarAsync(ct);
        if (sectionIdObj is not Guid sectionId)
        {
            await using var sel = conn.CreateCommand();
            sel.Transaction = tx;
            sel.CommandText = """
                SELECT id FROM procedures_config.form_sections
                WHERE procedure_type_id = @type_id AND section_key = @section_key LIMIT 1
                """;
            sel.Parameters.Add(new NpgsqlParameter("type_id", typeId));
            sel.Parameters.Add(new NpgsqlParameter("section_key", sectionKey));
            sectionId = (Guid)(await sel.ExecuteScalarAsync(ct))!;
        }

        if (edge.EdgeCode == "vehiculo")
        {
            await InsertFieldAsync(conn, tx, sectionId, "placa", "Placa", true, 1, true, ct);
            await InsertFieldAsync(conn, tx, sectionId, "tipo_documento", "Tipo de documento propietario", true, 2, false, ct);
            await InsertFieldAsync(conn, tx, sectionId, "doc_propietario", "Documento propietario", true, 3, false, ct);
            await InsertFieldAsync(conn, tx, sectionId, "vin", "VIN", false, 4, false, ct);
        }
        else if (edge.EdgeCode != "documentos")
        {
            var docKey = edge.EdgeCode switch
            {
                "propietario" => "doc_propietario",
                "comprador" => "doc_comprador",
                "locatario" => "doc_locatario",
                _ => $"doc_{edge.EdgeCode}",
            };
            await InsertFieldAsync(conn, tx, sectionId, "tipo_documento", "Tipo de documento", true, 1, false, ct);
            await InsertFieldAsync(conn, tx, sectionId, docKey, $"Documento — {title}", true, 2, true, ct);
        }
    }

    private static async Task InsertFieldAsync(
        System.Data.Common.DbConnection conn,
        System.Data.Common.DbTransaction tx,
        Guid sectionId,
        string fieldKey,
        string label,
        bool isRequired,
        int displayOrder,
        bool isTrigger,
        CancellationToken ct)
    {
        await using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = """
            INSERT INTO procedures_config.form_fields
              (section_id, field_key, data_type, label, is_required, display_order, ui_state, is_trigger, created_by, updated_by)
            VALUES
              (@section_id, @field_key, 'text', @label, @is_required, @display_order, 'vacio', @is_trigger, @user, @user)
            ON CONFLICT (section_id, field_key) DO NOTHING
            """;
        cmd.Parameters.Add(new NpgsqlParameter("section_id", sectionId));
        cmd.Parameters.Add(new NpgsqlParameter("field_key", fieldKey));
        cmd.Parameters.Add(new NpgsqlParameter("label", label));
        cmd.Parameters.Add(new NpgsqlParameter("is_required", isRequired));
        cmd.Parameters.Add(new NpgsqlParameter("display_order", displayOrder));
        cmd.Parameters.Add(new NpgsqlParameter("is_trigger", isTrigger));
        cmd.Parameters.Add(new NpgsqlParameter("user", SystemUserId));
        await cmd.ExecuteNonQueryAsync(ct);
    }

    private static async Task InsertTenantActivationAsync(
        System.Data.Common.DbConnection conn,
        System.Data.Common.DbTransaction tx,
        Guid tenantId,
        Guid typeId,
        Guid? trafficAgencyId,
        CancellationToken ct)
    {
        await using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = """
            INSERT INTO procedures_config.procedure_type_activations
              (tenant_id, procedure_type_id, traffic_agency_id, is_active, overrides, created_by, updated_by)
            SELECT @tenant_id, @type_id, @ot_id, TRUE, '{}'::jsonb, @user, @user
            WHERE NOT EXISTS (
              SELECT 1 FROM procedures_config.procedure_type_activations a
              WHERE a.tenant_id = @tenant_id
                AND a.procedure_type_id = @type_id
                AND a.deleted_at IS NULL
                AND (
                  (@ot_id IS NULL AND a.traffic_agency_id IS NULL)
                  OR a.traffic_agency_id = @ot_id
                )
            )
            """;
        cmd.Parameters.Add(new NpgsqlParameter("tenant_id", tenantId));
        cmd.Parameters.Add(new NpgsqlParameter("type_id", typeId));
        cmd.Parameters.Add(new NpgsqlParameter("ot_id", trafficAgencyId.HasValue ? trafficAgencyId.Value : DBNull.Value));
        cmd.Parameters.Add(new NpgsqlParameter("user", SystemUserId));
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<IReadOnlyList<AdminCatalogDocumentType>> ListCatalogDocumentTypesAsync(
        CancellationToken ct = default)
    {
        var conn = db.Database.GetDbConnection();
        if (conn.State != System.Data.ConnectionState.Open)
        {
            await conn.OpenAsync(ct);
        }

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT code, name, display_order, is_active
            FROM procedures_config.procedure_document_catalog
            ORDER BY display_order, name
            """;

        var list = new List<AdminCatalogDocumentType>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            list.Add(new AdminCatalogDocumentType(
                reader.GetString(0),
                reader.GetString(1),
                DefaultPersonKind: null,
                reader.GetInt32(2),
                reader.GetBoolean(3)));
        }

        return list;
    }

    public async Task<(AdminCatalogDocumentType? Ok, string? Error)> CreateCatalogDocumentTypeAsync(
        CreateCatalogDocumentTypeCommand command,
        CancellationToken ct = default)
    {
        var code = command.Code.Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(command.Name))
        {
            return (null, "code y name son obligatorios.");
        }

        if (command.TenantId == Guid.Empty)
        {
            return (null, "tenantId es requerido.");
        }

        var conn = await OpenWithConfigAdminContextAsync(command.TenantId, ct);

        var exists = await LoadProcedureDocumentCatalogIdAsync(conn, code, ct);
        if (exists is not null)
        {
            return (null, $"El código '{code}' ya existe en el catálogo.");
        }

        try
        {
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = """
                INSERT INTO procedures_config.procedure_document_catalog
                  (code, name, display_order, is_active, created_by, updated_by)
                VALUES
                  (@code, @name, @display_order, TRUE, @user, @user)
                RETURNING code, name, display_order
                """;
            cmd.Parameters.Add(new NpgsqlParameter("code", code));
            cmd.Parameters.Add(new NpgsqlParameter("name", command.Name.Trim()));
            cmd.Parameters.Add(new NpgsqlParameter("display_order", command.DisplayOrder));
            AddGuidParam(cmd, "user", SystemUserId);

            await using var reader = await cmd.ExecuteReaderAsync(ct);
            if (!await reader.ReadAsync(ct))
            {
                return (null, "No se pudo crear el documento en catálogo.");
            }

            return (new AdminCatalogDocumentType(
                reader.GetString(0),
                reader.GetString(1),
                null,
                reader.GetInt32(2),
                true), null);
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            return (null, $"El código '{code}' ya existe en el catálogo.");
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UndefinedTable)
        {
            return (null, "Tabla procedure_document_catalog no existe. Ejecute «pnpm migrate».");
        }
    }

    public async Task<(AdminCatalogDocumentType? Ok, string? Error)> UpdateCatalogDocumentTypeAsync(
        UpdateCatalogDocumentTypeCommand command,
        CancellationToken ct = default)
    {
        var code = command.Code.Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(code))
        {
            return (null, "code es obligatorio.");
        }

        if (command.TenantId == Guid.Empty)
        {
            return (null, "tenantId es requerido.");
        }

        if (command.Name is null && command.DisplayOrder is null && command.IsActive is null)
        {
            return (null, "Indique al menos un campo a actualizar.");
        }

        var conn = await OpenWithConfigAdminContextAsync(command.TenantId, ct);
        if (!await ProcedureDocumentCatalogCodeExistsAsync(conn, code, ct))
        {
            return (null, $"Documento '{code}' no existe en el catálogo.");
        }

        var sets = new List<string> { "updated_at = now()", "updated_by = @user" };
        if (command.Name is not null)
        {
            sets.Add("name = @name");
        }

        if (command.DisplayOrder.HasValue)
        {
            sets.Add("display_order = @display_order");
        }

        if (command.IsActive.HasValue)
        {
            sets.Add("is_active = @is_active");
        }

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = $"""
            UPDATE procedures_config.procedure_document_catalog
            SET {string.Join(", ", sets)}
            WHERE code = @code
            RETURNING code, name, display_order, is_active
            """;
        cmd.Parameters.Add(new NpgsqlParameter("code", code));
        AddGuidParam(cmd, "user", SystemUserId);
        if (command.Name is not null)
        {
            cmd.Parameters.Add(new NpgsqlParameter("name", command.Name.Trim()));
        }

        if (command.DisplayOrder.HasValue)
        {
            cmd.Parameters.Add(new NpgsqlParameter("display_order", command.DisplayOrder.Value));
        }

        if (command.IsActive.HasValue)
        {
            cmd.Parameters.Add(new NpgsqlParameter("is_active", command.IsActive.Value));
        }

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
        {
            return (null, "No se pudo actualizar el documento en catálogo.");
        }

        return (new AdminCatalogDocumentType(
            reader.GetString(0),
            reader.GetString(1),
            null,
            reader.GetInt32(2),
            reader.GetBoolean(3)), null);
    }

    public async Task<bool> DeactivateCatalogDocumentTypeAsync(
        Guid tenantId,
        string code,
        CancellationToken ct = default)
    {
        if (tenantId == Guid.Empty || string.IsNullOrWhiteSpace(code))
        {
            return false;
        }

        var conn = await OpenWithConfigAdminContextAsync(tenantId, ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            UPDATE procedures_config.procedure_document_catalog
            SET is_active = FALSE,
                updated_at = now(),
                updated_by = @user
            WHERE code = @code AND is_active = TRUE
            """;
        cmd.Parameters.Add(new NpgsqlParameter("code", code.Trim().ToUpperInvariant()));
        AddGuidParam(cmd, "user", SystemUserId);

        var rows = await cmd.ExecuteNonQueryAsync(ct);
        return rows > 0;
    }

    public async Task<(AdminRequiredDocumentItem? Ok, string? Error)> CreateRequiredDocumentAsync(
        CreateRequiredDocumentCommand command,
        CancellationToken ct = default)
    {
        if (command.Kind is not ("upload" or "auto_generated"))
        {
            return (null, "kind debe ser upload o auto_generated.");
        }

        if (string.IsNullOrWhiteSpace(command.ActorRole))
        {
            return (null, "actorRole es obligatorio (propietario, comprador, locatario, vehiculo, global).");
        }

        if (command.Kind == "auto_generated")
        {
            return (null, "Los documentos auto-generados requieren plantilla (document_templates). Use kind=upload por ahora.");
        }

        var actorRole = command.ActorRole.Trim().ToLowerInvariant();
        if (!IsAllowedActorRole(actorRole))
        {
            return (null, "actorRole inválido. Use: propietario, comprador, locatario, vehiculo o global.");
        }

        var conn = await OpenWithConfigAdminContextAsync(command.TenantId, ct);
        var typeId = await LoadTypeIdByCodeAsync(conn, command.ProcedureTypeCode, ct);
        if (typeId is null)
        {
            return (null, $"Tipo '{command.ProcedureTypeCode}' no encontrado.");
        }

        var edgeCode = string.IsNullOrWhiteSpace(command.EdgeCode) ? "documentos" : command.EdgeCode.Trim();
        Guid? edgeId;
        if (edgeCode == "documentos")
        {
            var (resolvedEdgeId, matrixError) = await EnsureDocumentosEdgeInMatrixAsync(conn, typeId.Value, ct);
            if (matrixError is not null)
            {
                return (null, matrixError);
            }

            edgeId = resolvedEdgeId;
        }
        else
        {
            edgeId = await LoadEdgeIdForTypeAsync(conn, typeId.Value, edgeCode, ct);
            if (edgeId is null)
            {
                return (null, $"Arista '{edgeCode}' no está en la matriz del tipo.");
            }
        }

        if (edgeId is null)
        {
            return (null, "No se pudo resolver la arista documentos para este trámite.");
        }

        var catalogCode = command.DocumentTypeCode.Trim().ToUpperInvariant();
        var docCatalogId = await LoadProcedureDocumentCatalogIdAsync(conn, catalogCode, ct);
        if (docCatalogId is null)
        {
            return (null, $"Documento '{catalogCode}' no existe en el catálogo. Regístrelo en Parametrización → Catálogo de documentos.");
        }

        if (await RequiredDocumentAssociationExistsAsync(conn, typeId.Value, docCatalogId.Value, actorRole, ct))
        {
            return (null, $"El documento '{catalogCode}' ya está asociado al trámite para el rol '{actorRole}'.");
        }

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO procedures_config.required_documents
              (procedure_type_id, edge_id, document_type_id, procedure_document_catalog_id, kind, is_required, display_order, actor_role, is_active, created_by, updated_by)
            VALUES
              (@type_id, @edge_id, NULL, @catalog_id, @kind, @is_required, @display_order, @actor_role, TRUE, @user, @user)
            RETURNING id
            """;
        AddGuidParam(cmd, "type_id", typeId.Value);
        AddGuidParam(cmd, "edge_id", edgeId.Value);
        AddGuidParam(cmd, "catalog_id", docCatalogId.Value);
        cmd.Parameters.Add(new NpgsqlParameter("kind", command.Kind));
        cmd.Parameters.Add(new NpgsqlParameter("is_required", command.IsRequired));
        cmd.Parameters.Add(new NpgsqlParameter("display_order", command.DisplayOrder));
        cmd.Parameters.Add(new NpgsqlParameter("actor_role", actorRole));
        AddGuidParam(cmd, "user", SystemUserId);

        Guid newId;
        try
        {
            newId = (Guid)(await cmd.ExecuteScalarAsync(ct))!;
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.ForeignKeyViolation)
        {
            return (null, "No se pudo asociar el documento: la arista documentos no está en la matriz del trámite.");
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.CheckViolation)
        {
            return (null, $"Restricción de datos al asociar documento: {ex.MessageText}");
        }

        var items = await LoadRequiredDocumentsAdminAsync(conn, typeId.Value, ct);
        var created = items.FirstOrDefault(d => d.Id == newId);
        return created is null ? (null, "Documento creado pero no se pudo recargar.") : (created, null);
    }

    public async Task<(AdminRequiredDocumentItem? Ok, string? Error)> UpdateRequiredDocumentAsync(
        UpdateRequiredDocumentCommand command,
        CancellationToken ct = default)
    {
        if (command.Kind is not null and not ("upload" or "auto_generated"))
        {
            return (null, "kind debe ser upload o auto_generated.");
        }

        var conn = await OpenWithConfigAdminContextAsync(command.TenantId, ct);
        var typeId = await LoadTypeIdByCodeAsync(conn, command.ProcedureTypeCode, ct);
        if (typeId is null)
        {
            return (null, $"Tipo '{command.ProcedureTypeCode}' no encontrado.");
        }

        Guid? edgeId = null;
        if (!string.IsNullOrWhiteSpace(command.EdgeCode))
        {
            edgeId = await LoadEdgeIdForTypeAsync(conn, typeId.Value, command.EdgeCode!, ct);
            if (edgeId is null)
            {
                return (null, $"Arista '{command.EdgeCode}' no está en la matriz del tipo.");
            }
        }

        var sets = new List<string> { "updated_at = now()", "updated_by = @user" };
        if (edgeId.HasValue)
        {
            sets.Add("edge_id = @edge_id");
        }

        if (command.Kind is not null)
        {
            sets.Add("kind = @kind");
        }

        if (command.IsRequired.HasValue)
        {
            sets.Add("is_required = @is_required");
        }

        if (command.DisplayOrder.HasValue)
        {
            sets.Add("display_order = @display_order");
        }

        if (command.ActorRole is not null)
        {
            sets.Add("actor_role = @actor_role");
        }

        if (command.IsActive.HasValue)
        {
            sets.Add("is_active = @is_active");
        }

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = $"""
            UPDATE procedures_config.required_documents
            SET {string.Join(", ", sets)}
            WHERE id = @id AND procedure_type_id = @type_id
            """;
        AddGuidParam(cmd, "id", command.DocumentId);
        AddGuidParam(cmd, "type_id", typeId.Value);
        AddGuidParam(cmd, "user", SystemUserId);
        if (edgeId.HasValue)
        {
            AddGuidParam(cmd, "edge_id", edgeId.Value);
        }

        if (command.Kind is not null)
        {
            cmd.Parameters.Add(new NpgsqlParameter("kind", command.Kind));
        }

        if (command.IsRequired.HasValue)
        {
            cmd.Parameters.Add(new NpgsqlParameter("is_required", command.IsRequired.Value));
        }

        if (command.DisplayOrder.HasValue)
        {
            cmd.Parameters.Add(new NpgsqlParameter("display_order", command.DisplayOrder.Value));
        }

        if (command.ActorRole is not null)
        {
            cmd.Parameters.Add(new NpgsqlParameter("actor_role", command.ActorRole));
        }

        if (command.IsActive.HasValue)
        {
            cmd.Parameters.Add(new NpgsqlParameter("is_active", command.IsActive.Value));
        }

        var rows = await cmd.ExecuteNonQueryAsync(ct);
        if (rows == 0)
        {
            return (null, "Documento no encontrado para este tipo.");
        }

        var items = await LoadRequiredDocumentsAdminAsync(conn, typeId.Value, ct);
        var updated = items.FirstOrDefault(d => d.Id == command.DocumentId);
        return updated is null ? (null, "Documento actualizado pero no se pudo recargar.") : (updated, null);
    }

    public async Task<bool> DeactivateRequiredDocumentAsync(
        Guid tenantId,
        string procedureTypeCode,
        Guid documentId,
        CancellationToken ct = default)
    {
        var conn = await OpenWithConfigAdminContextAsync(tenantId, ct);
        var typeId = await LoadTypeIdByCodeAsync(conn, procedureTypeCode, ct);
        if (typeId is null)
        {
            return false;
        }

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            UPDATE procedures_config.required_documents
            SET is_active = FALSE,
                updated_at = now(),
                updated_by = @user
            WHERE id = @id AND procedure_type_id = @type_id
            """;
        AddGuidParam(cmd, "id", documentId);
        AddGuidParam(cmd, "type_id", typeId.Value);
        AddGuidParam(cmd, "user", SystemUserId);

        var rows = await cmd.ExecuteNonQueryAsync(ct);
        return rows > 0;
    }

    private static async Task InsertDefaultRequiredDocumentAsync(
        System.Data.Common.DbConnection conn,
        System.Data.Common.DbTransaction tx,
        Guid typeId,
        Guid documentosEdgeId,
        string actorRole,
        int displayOrder,
        CancellationToken ct)
    {
        await using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = """
            INSERT INTO procedures_config.required_documents
              (procedure_type_id, edge_id, document_type_id, kind, is_required, display_order, actor_role, is_active, created_by, updated_by)
            SELECT @type_id, @edge_id, dt.id, 'upload', TRUE, @display_order, @actor_role, TRUE, @user, @user
            FROM catalogs.document_types dt
            WHERE dt.code = 'CC'
              AND NOT EXISTS (
                SELECT 1 FROM procedures_config.required_documents rd
                WHERE rd.procedure_type_id = @type_id
                  AND rd.actor_role = @actor_role
                  AND rd.kind = 'upload'
              )
            LIMIT 1
            """;
        cmd.Parameters.Add(new NpgsqlParameter("type_id", typeId));
        cmd.Parameters.Add(new NpgsqlParameter("edge_id", documentosEdgeId));
        cmd.Parameters.Add(new NpgsqlParameter("display_order", displayOrder));
        cmd.Parameters.Add(new NpgsqlParameter("actor_role", actorRole));
        cmd.Parameters.Add(new NpgsqlParameter("user", SystemUserId));
        await cmd.ExecuteNonQueryAsync(ct);
    }

    private static async Task<Guid?> LoadGlobalEdgeIdAsync(
        System.Data.Common.DbConnection conn,
        string edgeCode,
        CancellationToken ct)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT id FROM procedures_config.edges WHERE code = @code LIMIT 1";
        cmd.Parameters.Add(new NpgsqlParameter("code", edgeCode));
        var result = await cmd.ExecuteScalarAsync(ct);
        return result is Guid g ? g : null;
    }

    private static async Task<Guid?> LoadGlobalEdgeIdAsync(
        System.Data.Common.DbConnection conn,
        System.Data.Common.DbTransaction tx,
        string edgeCode,
        CancellationToken ct)
    {
        await using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = "SELECT id FROM procedures_config.edges WHERE code = @code LIMIT 1";
        cmd.Parameters.Add(new NpgsqlParameter("code", edgeCode));
        var result = await cmd.ExecuteScalarAsync(ct);
        return result is Guid g ? g : null;
    }

    private static async Task<Guid?> LoadEdgeIdForTypeAsync(
        System.Data.Common.DbConnection conn,
        Guid typeId,
        string edgeCode,
        CancellationToken ct)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT m.edge_id
            FROM procedures_config.procedure_type_edges m
            JOIN procedures_config.edges e ON e.id = m.edge_id
            WHERE m.procedure_type_id = @type_id AND e.code = @edge_code
            LIMIT 1
            """;
        AddGuidParam(cmd, "type_id", typeId);
        cmd.Parameters.Add(new NpgsqlParameter("edge_code", edgeCode));
        var result = await cmd.ExecuteScalarAsync(ct);
        return result is Guid g ? g : null;
    }

    /// <summary>
    /// Garantiza fila en procedure_type_edges para documentos (FK required_documents → matriz).
    /// </summary>
    private static async Task<(Guid? EdgeId, string? Error)> EnsureDocumentosEdgeInMatrixAsync(
        System.Data.Common.DbConnection conn,
        Guid typeId,
        CancellationToken ct)
    {
        var existing = await LoadEdgeIdForTypeAsync(conn, typeId, "documentos", ct);
        if (existing is not null)
        {
            return (existing, null);
        }

        var globalEdgeId = await LoadGlobalEdgeIdAsync(conn, "documentos", ct);
        if (globalEdgeId is null)
        {
            return (null, "La arista documentos no existe en catálogo. Ejecute «pnpm migrate».");
        }

        var displayOrder = await LoadNextMatrixDisplayOrderAsync(conn, typeId, ct);

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO procedures_config.procedure_type_edges
              (procedure_type_id, edge_id, is_active, is_required, display_order, role_label, created_by, updated_by)
            VALUES
              (@type_id, @edge_id, TRUE, FALSE, @display_order, 'Documentos', @user, @user)
            ON CONFLICT (procedure_type_id, edge_id) DO UPDATE SET
              updated_at = now(),
              updated_by = EXCLUDED.updated_by
            """;
        AddGuidParam(cmd, "type_id", typeId);
        AddGuidParam(cmd, "edge_id", globalEdgeId.Value);
        cmd.Parameters.Add(new NpgsqlParameter("display_order", displayOrder));
        AddGuidParam(cmd, "user", SystemUserId);
        await cmd.ExecuteNonQueryAsync(ct);

        return (globalEdgeId, null);
    }

    private static async Task<int> LoadNextMatrixDisplayOrderAsync(
        System.Data.Common.DbConnection conn,
        Guid typeId,
        CancellationToken ct)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT COALESCE(MAX(display_order), 0) + 1
            FROM procedures_config.procedure_type_edges
            WHERE procedure_type_id = @type_id
            """;
        AddGuidParam(cmd, "type_id", typeId);
        var result = await cmd.ExecuteScalarAsync(ct);
        return result is int i ? i : 1;
    }

    private static bool IsAllowedActorRole(string actorRole) =>
        actorRole is "propietario" or "comprador" or "locatario" or "vehiculo" or "global";

    private static async Task<bool> RequiredDocumentAssociationExistsAsync(
        System.Data.Common.DbConnection conn,
        Guid typeId,
        Guid catalogId,
        string actorRole,
        CancellationToken ct)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT 1
            FROM procedures_config.required_documents rd
            WHERE rd.procedure_type_id = @type_id
              AND rd.procedure_document_catalog_id = @catalog_id
              AND rd.actor_role = @actor_role
              AND rd.is_active = TRUE
            LIMIT 1
            """;
        AddGuidParam(cmd, "type_id", typeId);
        AddGuidParam(cmd, "catalog_id", catalogId);
        cmd.Parameters.Add(new NpgsqlParameter("actor_role", actorRole));
        var result = await cmd.ExecuteScalarAsync(ct);
        return result is not null;
    }

    private static async Task<bool> ProcedureDocumentCatalogCodeExistsAsync(
        System.Data.Common.DbConnection conn,
        string documentCode,
        CancellationToken ct)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT 1 FROM procedures_config.procedure_document_catalog
            WHERE code = @code
            LIMIT 1
            """;
        cmd.Parameters.Add(new NpgsqlParameter("code", documentCode.Trim().ToUpperInvariant()));
        var result = await cmd.ExecuteScalarAsync(ct);
        return result is not null;
    }

    private static async Task<Guid?> LoadProcedureDocumentCatalogIdAsync(
        System.Data.Common.DbConnection conn,
        string documentCode,
        CancellationToken ct)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT id FROM procedures_config.procedure_document_catalog
            WHERE code = @code AND is_active = TRUE
            LIMIT 1
            """;
        cmd.Parameters.Add(new NpgsqlParameter("code", documentCode.Trim().ToUpperInvariant()));
        var result = await cmd.ExecuteScalarAsync(ct);
        return result is Guid g ? g : null;
    }

    private static async Task<Guid?> LoadDocumentTypeIdAsync(
        System.Data.Common.DbConnection conn,
        string documentTypeCode,
        CancellationToken ct)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT id FROM catalogs.document_types WHERE code = @code LIMIT 1";
        cmd.Parameters.Add(new NpgsqlParameter("code", documentTypeCode.Trim().ToUpperInvariant()));
        var result = await cmd.ExecuteScalarAsync(ct);
        return result is Guid g ? g : null;
    }

    public async Task<IReadOnlyList<AdminCatalogEdge>> ListCatalogQueryConnectorsAsync(CancellationToken ct = default)
    {
        var conn = db.Database.GetDbConnection();
        if (conn.State != System.Data.ConnectionState.Open)
        {
            await conn.OpenAsync(ct);
        }

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT code, name, 'connector', 0
            FROM procedures_config.query_connectors
            WHERE is_active = TRUE
            ORDER BY name
            """;

        var list = new List<AdminCatalogEdge>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            list.Add(new AdminCatalogEdge(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetInt32(3)));
        }

        return list;
    }

    public async Task<IReadOnlyList<AdminQueryConfigItem>> ListQueryConfigsAsync(
        Guid tenantId,
        string procedureTypeCode,
        CancellationToken ct = default)
    {
        var conn = await OpenWithConfigAdminContextAsync(tenantId, ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT qc.id, e.code, conn.code, conn.name,
                   qc.is_mandatory, qc.is_omitible, qc.person_kind_filter, qc.display_order
            FROM procedures_config.procedure_type_query_configs qc
            JOIN procedures_config.edges e ON e.id = qc.edge_id
            JOIN procedures_config.query_connectors conn ON conn.id = qc.query_connector_id
            JOIN procedures_config.procedure_types t ON t.id = qc.procedure_type_id
            WHERE t.code = @type_code
            ORDER BY e.display_order, qc.display_order
            """;
        cmd.Parameters.Add(new NpgsqlParameter("type_code", procedureTypeCode));
        var list = new List<AdminQueryConfigItem>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            list.Add(new AdminQueryConfigItem(
                reader.GetGuid(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetString(3),
                reader.GetBoolean(4),
                reader.GetBoolean(5),
                reader.GetString(6),
                reader.GetInt32(7)));
        }

        return list;
    }

    public async Task<(AdminQueryConfigItem? Ok, string? Error)> CreateQueryConfigAsync(
        CreateQueryConfigCommand command,
        CancellationToken ct = default)
    {
        if (command.PersonKindFilter is not ("any" or "natural" or "juridica"))
        {
            return (null, "personKindFilter debe ser any, natural o juridica.");
        }

        var conn = await OpenWithConfigAdminContextAsync(command.TenantId, ct);
        var typeId = await LoadTypeIdByCodeAsync(conn, command.ProcedureTypeCode, ct);
        if (typeId is null)
        {
            return (null, $"Tipo '{command.ProcedureTypeCode}' no encontrado.");
        }

        var edgeId = await LoadEdgeIdForTypeAsync(conn, typeId.Value, command.EdgeCode, ct);
        if (edgeId is null)
        {
            edgeId = await LoadGlobalEdgeIdAsync(conn, command.EdgeCode, ct);
            if (edgeId is null)
            {
                return (null, $"Arista '{command.EdgeCode}' no existe.");
            }
        }

        await using var connectorCmd = conn.CreateCommand();
        connectorCmd.CommandText = "SELECT id FROM procedures_config.query_connectors WHERE code = @code LIMIT 1";
        connectorCmd.Parameters.Add(new NpgsqlParameter("code", command.ConnectorCode.Trim().ToUpperInvariant()));
        var connectorIdObj = await connectorCmd.ExecuteScalarAsync(ct);
        if (connectorIdObj is not Guid connectorId)
        {
            return (null, $"Conector '{command.ConnectorCode}' no existe en catálogo.");
        }

        await using var insertCmd = conn.CreateCommand();
        insertCmd.CommandText = """
            INSERT INTO procedures_config.procedure_type_query_configs
              (procedure_type_id, edge_id, query_connector_id, is_mandatory, is_omitible, person_kind_filter, display_order, created_by, updated_by)
            VALUES
              (@type_id, @edge_id, @connector_id, @is_mandatory, @is_omitible, @person_kind_filter, @display_order, @user, @user)
            ON CONFLICT (procedure_type_id, edge_id, query_connector_id, person_kind_filter) DO UPDATE SET
              is_mandatory = EXCLUDED.is_mandatory,
              is_omitible = EXCLUDED.is_omitible,
              display_order = EXCLUDED.display_order,
              updated_by = EXCLUDED.updated_by,
              updated_at = now()
            RETURNING id
            """;
        AddGuidParam(insertCmd, "type_id", typeId.Value);
        AddGuidParam(insertCmd, "edge_id", edgeId.Value);
        insertCmd.Parameters.Add(new NpgsqlParameter("connector_id", connectorId));
        insertCmd.Parameters.Add(new NpgsqlParameter("is_mandatory", command.IsMandatory));
        insertCmd.Parameters.Add(new NpgsqlParameter("is_omitible", command.IsOmitible));
        insertCmd.Parameters.Add(new NpgsqlParameter("person_kind_filter", command.PersonKindFilter));
        insertCmd.Parameters.Add(new NpgsqlParameter("display_order", command.DisplayOrder));
        AddGuidParam(insertCmd, "user", SystemUserId);

        var newId = (Guid)(await insertCmd.ExecuteScalarAsync(ct))!;
        var all = await ListQueryConfigsAsync(command.TenantId, command.ProcedureTypeCode, ct);
        var created = all.FirstOrDefault(q => q.Id == newId);
        return created is null ? (null, "Configuración creada pero no se pudo recargar.") : (created, null);
    }

    public async Task<(AdminQueryConfigItem? Ok, string? Error)> UpdateQueryConfigAsync(
        UpdateQueryConfigCommand command,
        CancellationToken ct = default)
    {
        if (command.PersonKindFilter is not null and not ("any" or "natural" or "juridica"))
        {
            return (null, "personKindFilter debe ser any, natural o juridica.");
        }

        var conn = await OpenWithConfigAdminContextAsync(command.TenantId, ct);
        var typeId = await LoadTypeIdByCodeAsync(conn, command.ProcedureTypeCode, ct);
        if (typeId is null)
        {
            return (null, $"Tipo '{command.ProcedureTypeCode}' no encontrado.");
        }

        var sets = new List<string> { "updated_at = now()", "updated_by = @user" };
        if (command.IsMandatory.HasValue) sets.Add("is_mandatory = @is_mandatory");
        if (command.IsOmitible.HasValue) sets.Add("is_omitible = @is_omitible");
        if (command.PersonKindFilter is not null) sets.Add("person_kind_filter = @person_kind_filter");
        if (command.DisplayOrder.HasValue) sets.Add("display_order = @display_order");

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = $"""
            UPDATE procedures_config.procedure_type_query_configs
            SET {string.Join(", ", sets)}
            WHERE id = @id AND procedure_type_id = @type_id
            """;
        AddGuidParam(cmd, "id", command.QueryConfigId);
        AddGuidParam(cmd, "type_id", typeId.Value);
        AddGuidParam(cmd, "user", SystemUserId);
        if (command.IsMandatory.HasValue)
            cmd.Parameters.Add(new NpgsqlParameter("is_mandatory", command.IsMandatory.Value));
        if (command.IsOmitible.HasValue)
            cmd.Parameters.Add(new NpgsqlParameter("is_omitible", command.IsOmitible.Value));
        if (command.PersonKindFilter is not null)
            cmd.Parameters.Add(new NpgsqlParameter("person_kind_filter", command.PersonKindFilter));
        if (command.DisplayOrder.HasValue)
            cmd.Parameters.Add(new NpgsqlParameter("display_order", command.DisplayOrder.Value));

        var rows = await cmd.ExecuteNonQueryAsync(ct);
        if (rows == 0)
        {
            return (null, "Configuración no encontrada para este tipo.");
        }

        var all = await ListQueryConfigsAsync(command.TenantId, command.ProcedureTypeCode, ct);
        var updated = all.FirstOrDefault(q => q.Id == command.QueryConfigId);
        return updated is null ? (null, "Configuración actualizada pero no se pudo recargar.") : (updated, null);
    }

    public async Task<bool> DeleteQueryConfigAsync(
        Guid tenantId,
        string procedureTypeCode,
        Guid queryConfigId,
        CancellationToken ct = default)
    {
        var conn = await OpenWithConfigAdminContextAsync(tenantId, ct);
        var typeId = await LoadTypeIdByCodeAsync(conn, procedureTypeCode, ct);
        if (typeId is null)
        {
            return false;
        }

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            DELETE FROM procedures_config.procedure_type_query_configs
            WHERE id = @id AND procedure_type_id = @type_id
            """;
        AddGuidParam(cmd, "id", queryConfigId);
        AddGuidParam(cmd, "type_id", typeId.Value);

        var rows = await cmd.ExecuteNonQueryAsync(ct);
        return rows > 0;
    }

    public async Task<AdminEdgeFormView?> GetFormByEdgeAsync(
        Guid tenantId,
        string procedureTypeCode,
        string edgeCode,
        CancellationToken ct = default)
    {
        var conn = await OpenWithConfigAdminContextAsync(tenantId, ct);
        var typeRow = await LoadTypeRowAdminAsync(conn, procedureTypeCode, ct);
        if (typeRow is null)
        {
            return null;
        }

        var sections = await LoadSectionsForEdgeAdminAsync(conn, typeRow.Id, edgeCode, ct);
        return new AdminEdgeFormView(procedureTypeCode, edgeCode, sections);
    }

    public async Task<(AdminFormSectionItem? Ok, string? Error)> CreateFormSectionAsync(
        CreateFormSectionCommand command,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(command.SectionKey) || string.IsNullOrWhiteSpace(command.Title))
        {
            return (null, "sectionKey y title son obligatorios.");
        }

        if (command.UiMode is not ("read_only" or "interactive"))
        {
            return (null, "uiMode debe ser read_only o interactive.");
        }

        var conn = await OpenWithConfigAdminContextAsync(command.TenantId, ct);
        var typeId = await LoadTypeIdByCodeAsync(conn, command.ProcedureTypeCode, ct);
        if (typeId is null)
        {
            return (null, "Tipo de trámite no encontrado.");
        }

        var edgeId = await LoadEdgeIdForTypeAsync(conn, typeId.Value, command.EdgeCode, ct);
        if (edgeId is null)
        {
            return (null, "Arista no encontrada en la matriz del trámite.");
        }

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO procedures_config.form_sections
              (procedure_type_id, edge_id, section_key, title, display_order, ui_mode, is_active, created_by, updated_by)
            VALUES
              (@type_id, @edge_id, @section_key, @title, @display_order, @ui_mode, TRUE, @user, @user)
            RETURNING id, section_key, title, display_order, ui_mode, row_version
            """;
        AddGuidParam(cmd, "type_id", typeId.Value);
        AddGuidParam(cmd, "edge_id", edgeId.Value);
        cmd.Parameters.Add(new NpgsqlParameter("section_key", command.SectionKey.Trim()));
        cmd.Parameters.Add(new NpgsqlParameter("title", command.Title.Trim()));
        cmd.Parameters.Add(new NpgsqlParameter("display_order", command.DisplayOrder));
        cmd.Parameters.Add(new NpgsqlParameter("ui_mode", command.UiMode));
        AddGuidParam(cmd, "user", SystemUserId);

        try
        {
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            if (!await reader.ReadAsync(ct))
            {
                return (null, "No se pudo crear la sección.");
            }

            return (new AdminFormSectionItem(
                reader.GetGuid(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetInt32(3),
                reader.GetString(4),
                command.EdgeCode,
                reader.GetInt32(5),
                []), null);
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            return (null, "Ya existe una sección con esa clave.");
        }
    }

    public async Task<(AdminFormSectionItem? Ok, string? Error)> UpdateFormSectionAsync(
        UpdateFormSectionCommand command,
        CancellationToken ct = default)
    {
        if (command.Title is null && command.DisplayOrder is null && command.UiMode is null)
        {
            return (null, "Indique al menos un campo a actualizar.");
        }

        if (command.UiMode is not null && command.UiMode is not ("read_only" or "interactive"))
        {
            return (null, "uiMode debe ser read_only o interactive.");
        }

        var conn = await OpenWithConfigAdminContextAsync(command.TenantId, ct);
        var typeId = await LoadTypeIdByCodeAsync(conn, command.ProcedureTypeCode, ct);
        if (typeId is null)
        {
            return (null, "Tipo de trámite no encontrado.");
        }

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            UPDATE procedures_config.form_sections
            SET title = COALESCE(@title, title),
                display_order = COALESCE(@display_order, display_order),
                ui_mode = COALESCE(@ui_mode, ui_mode),
                updated_by = @user,
                updated_at = now()
            WHERE id = @section_id
              AND procedure_type_id = @type_id
              AND is_active = TRUE
            RETURNING id, section_key, title, display_order, ui_mode, row_version,
              (SELECT e.code FROM procedures_config.edges e WHERE e.id = form_sections.edge_id)
            """;
        AddGuidParam(cmd, "section_id", command.SectionId);
        AddGuidParam(cmd, "type_id", typeId.Value);
        cmd.Parameters.Add(new NpgsqlParameter("title", (object?)command.Title?.Trim() ?? DBNull.Value));
        cmd.Parameters.Add(new NpgsqlParameter("display_order", (object?)command.DisplayOrder ?? DBNull.Value));
        cmd.Parameters.Add(new NpgsqlParameter("ui_mode", (object?)command.UiMode ?? DBNull.Value));
        AddGuidParam(cmd, "user", SystemUserId);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
        {
            return (null, "Sección no encontrada.");
        }

        var sectionId = reader.GetGuid(0);
        var fields = await LoadAdminFormFieldsAsync(conn, sectionId, ct);
        return (new AdminFormSectionItem(
            sectionId,
            reader.GetString(1),
            reader.GetString(2),
            reader.GetInt32(3),
            reader.GetString(4),
            reader.GetString(5),
            reader.GetInt32(6),
            fields), null);
    }

    public async Task<bool> DeactivateFormSectionAsync(
        Guid tenantId,
        string procedureTypeCode,
        Guid sectionId,
        CancellationToken ct = default)
    {
        var conn = await OpenWithConfigAdminContextAsync(tenantId, ct);
        var typeId = await LoadTypeIdByCodeAsync(conn, procedureTypeCode, ct);
        if (typeId is null)
        {
            return false;
        }

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            UPDATE procedures_config.form_sections
            SET is_active = FALSE, updated_by = @user, updated_at = now()
            WHERE id = @section_id AND procedure_type_id = @type_id AND is_active = TRUE
            """;
        AddGuidParam(cmd, "section_id", sectionId);
        AddGuidParam(cmd, "type_id", typeId.Value);
        AddGuidParam(cmd, "user", SystemUserId);
        return await cmd.ExecuteNonQueryAsync(ct) > 0;
    }

    public async Task<(AdminFormFieldItem? Ok, string? Error)> CreateFormFieldAsync(
        CreateFormFieldCommand command,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(command.FieldKey) || string.IsNullOrWhiteSpace(command.Label))
        {
            return (null, "fieldKey y label son obligatorios.");
        }

        if (!IsValidFormFieldDataType(command.DataType))
        {
            return (null, "dataType inválido.");
        }

        if (!IsValidFormFieldUiState(command.UiState))
        {
            return (null, "uiState inválido.");
        }

        var conn = await OpenWithConfigAdminContextAsync(command.TenantId, ct);
        var typeId = await LoadTypeIdByCodeAsync(conn, command.ProcedureTypeCode, ct);
        if (typeId is null)
        {
            return (null, "Tipo de trámite no encontrado.");
        }

        if (!await FormSectionBelongsToTypeAsync(conn, command.SectionId, typeId.Value, ct))
        {
            return (null, "Sección no encontrada.");
        }

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO procedures_config.form_fields
              (section_id, field_key, data_type, label, is_required, display_order, ui_state, is_trigger,
               validation, options, is_active, created_by, updated_by)
            VALUES
              (@section_id, @field_key, @data_type, @label, @is_required, @display_order, @ui_state, @is_trigger,
               @validation::jsonb, @options::jsonb, TRUE, @user, @user)
            RETURNING id, field_key, data_type, label, is_required, display_order, ui_state, is_trigger,
                      validation::text, options::text, row_version
            """;
        AddGuidParam(cmd, "section_id", command.SectionId);
        cmd.Parameters.Add(new NpgsqlParameter("field_key", command.FieldKey.Trim()));
        cmd.Parameters.Add(new NpgsqlParameter("data_type", command.DataType));
        cmd.Parameters.Add(new NpgsqlParameter("label", command.Label.Trim()));
        cmd.Parameters.Add(new NpgsqlParameter("is_required", command.IsRequired));
        cmd.Parameters.Add(new NpgsqlParameter("display_order", command.DisplayOrder));
        cmd.Parameters.Add(new NpgsqlParameter("ui_state", command.UiState));
        cmd.Parameters.Add(new NpgsqlParameter("is_trigger", command.IsTrigger));
        cmd.Parameters.Add(new NpgsqlParameter("validation", command.ValidationJson ?? "{}"));
        cmd.Parameters.Add(new NpgsqlParameter("options", command.OptionsJson ?? "[]"));
        AddGuidParam(cmd, "user", SystemUserId);

        try
        {
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            if (!await reader.ReadAsync(ct))
            {
                return (null, "No se pudo crear el campo.");
            }

            return (ReadAdminFormField(reader), null);
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            return (null, "Ya existe un campo con esa clave en la sección.");
        }
    }

    public async Task<(AdminFormFieldItem? Ok, string? Error)> UpdateFormFieldAsync(
        UpdateFormFieldCommand command,
        CancellationToken ct = default)
    {
        if (command.Label is null && command.IsRequired is null && command.DisplayOrder is null &&
            command.UiState is null && command.IsTrigger is null &&
            command.ValidationJson is null && command.OptionsJson is null)
        {
            return (null, "Indique al menos un campo a actualizar.");
        }

        if (command.UiState is not null && !IsValidFormFieldUiState(command.UiState))
        {
            return (null, "uiState inválido.");
        }

        var conn = await OpenWithConfigAdminContextAsync(command.TenantId, ct);
        var typeId = await LoadTypeIdByCodeAsync(conn, command.ProcedureTypeCode, ct);
        if (typeId is null)
        {
            return (null, "Tipo de trámite no encontrado.");
        }

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            UPDATE procedures_config.form_fields ff
            SET label = COALESCE(@label, label),
                is_required = COALESCE(@is_required, is_required),
                display_order = COALESCE(@display_order, display_order),
                ui_state = COALESCE(@ui_state, ui_state),
                is_trigger = COALESCE(@is_trigger, is_trigger),
                validation = COALESCE(@validation::jsonb, validation),
                options = COALESCE(@options::jsonb, options),
                updated_by = @user,
                updated_at = now()
            FROM procedures_config.form_sections s
            WHERE ff.id = @field_id
              AND ff.section_id = s.id
              AND s.procedure_type_id = @type_id
              AND ff.is_active = TRUE
            RETURNING ff.id, ff.field_key, ff.data_type, ff.label, ff.is_required, ff.display_order,
                      ff.ui_state, ff.is_trigger, ff.validation::text, ff.options::text, ff.row_version
            """;
        AddGuidParam(cmd, "field_id", command.FieldId);
        AddGuidParam(cmd, "type_id", typeId.Value);
        cmd.Parameters.Add(new NpgsqlParameter("label", (object?)command.Label?.Trim() ?? DBNull.Value));
        cmd.Parameters.Add(new NpgsqlParameter("is_required", (object?)command.IsRequired ?? DBNull.Value));
        cmd.Parameters.Add(new NpgsqlParameter("display_order", (object?)command.DisplayOrder ?? DBNull.Value));
        cmd.Parameters.Add(new NpgsqlParameter("ui_state", (object?)command.UiState ?? DBNull.Value));
        cmd.Parameters.Add(new NpgsqlParameter("is_trigger", (object?)command.IsTrigger ?? DBNull.Value));
        cmd.Parameters.Add(new NpgsqlParameter("validation", (object?)command.ValidationJson ?? DBNull.Value));
        cmd.Parameters.Add(new NpgsqlParameter("options", (object?)command.OptionsJson ?? DBNull.Value));
        AddGuidParam(cmd, "user", SystemUserId);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
        {
            return (null, "Campo no encontrado.");
        }

        return (ReadAdminFormField(reader), null);
    }

    public async Task<bool> DeactivateFormFieldAsync(
        Guid tenantId,
        string procedureTypeCode,
        Guid fieldId,
        CancellationToken ct = default)
    {
        var conn = await OpenWithConfigAdminContextAsync(tenantId, ct);
        var typeId = await LoadTypeIdByCodeAsync(conn, procedureTypeCode, ct);
        if (typeId is null)
        {
            return false;
        }

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            UPDATE procedures_config.form_fields ff
            SET is_active = FALSE, updated_by = @user, updated_at = now()
            FROM procedures_config.form_sections s
            WHERE ff.id = @field_id
              AND ff.section_id = s.id
              AND s.procedure_type_id = @type_id
              AND ff.is_active = TRUE
            """;
        AddGuidParam(cmd, "field_id", fieldId);
        AddGuidParam(cmd, "type_id", typeId.Value);
        AddGuidParam(cmd, "user", SystemUserId);
        return await cmd.ExecuteNonQueryAsync(ct) > 0;
    }

    private static async Task<IReadOnlyList<AdminFormSectionItem>> LoadSectionsForEdgeAdminAsync(
        System.Data.Common.DbConnection conn,
        Guid typeId,
        string edgeCode,
        CancellationToken ct)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT s.id, s.section_key, s.title, s.display_order, s.ui_mode, s.row_version, ed.code
            FROM procedures_config.form_sections s
            JOIN procedures_config.edges ed ON ed.id = s.edge_id
            WHERE s.procedure_type_id = @type_id
              AND ed.code = @edge_code
              AND s.is_active = TRUE
            ORDER BY s.display_order
            """;
        AddGuidParam(cmd, "type_id", typeId);
        cmd.Parameters.Add(new NpgsqlParameter("edge_code", edgeCode));

        var sections = new List<(Guid Id, AdminFormSectionItem Section)>();
        await using (var reader = await cmd.ExecuteReaderAsync(ct))
        {
            while (await reader.ReadAsync(ct))
            {
                sections.Add((
                    reader.GetGuid(0),
                    new AdminFormSectionItem(
                        reader.GetGuid(0),
                        reader.GetString(1),
                        reader.GetString(2),
                        reader.GetInt32(3),
                        reader.GetString(4),
                        reader.GetString(6),
                        reader.GetInt32(5),
                        [])));
            }
        }

        var result = new List<AdminFormSectionItem>();
        foreach (var (sectionId, section) in sections)
        {
            var fields = await LoadAdminFormFieldsAsync(conn, sectionId, ct);
            result.Add(section with { Fields = fields });
        }

        return result;
    }

    private static async Task<IReadOnlyList<AdminFormFieldItem>> LoadAdminFormFieldsAsync(
        System.Data.Common.DbConnection conn,
        Guid sectionId,
        CancellationToken ct)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT id, field_key, data_type, label, is_required, display_order, ui_state, is_trigger,
                   validation::text, options::text, row_version
            FROM procedures_config.form_fields
            WHERE section_id = @section_id AND is_active = TRUE
            ORDER BY display_order
            """;
        AddGuidParam(cmd, "section_id", sectionId);
        var list = new List<AdminFormFieldItem>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            list.Add(ReadAdminFormField(reader));
        }

        return list;
    }

    private static AdminFormFieldItem ReadAdminFormField(System.Data.Common.DbDataReader reader) =>
        new(
            reader.GetGuid(0),
            reader.GetString(1),
            reader.GetString(2),
            reader.GetString(3),
            reader.GetBoolean(4),
            reader.GetInt32(5),
            reader.GetString(6),
            reader.GetBoolean(7),
            ParseJsonObject(reader.GetString(8)),
            ParseJsonArray(reader.GetString(9)),
            reader.GetInt32(10));

    private static async Task<bool> FormSectionBelongsToTypeAsync(
        System.Data.Common.DbConnection conn,
        Guid sectionId,
        Guid typeId,
        CancellationToken ct)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT 1 FROM procedures_config.form_sections
            WHERE id = @section_id AND procedure_type_id = @type_id AND is_active = TRUE
            LIMIT 1
            """;
        AddGuidParam(cmd, "section_id", sectionId);
        AddGuidParam(cmd, "type_id", typeId);
        return await cmd.ExecuteScalarAsync(ct) is not null;
    }

    private static bool IsValidFormFieldDataType(string dataType) =>
        dataType is "text" or "number" or "date" or "boolean" or "select" or "multiselect" or "file";

    private static bool IsValidFormFieldUiState(string uiState) =>
        uiState is "vacio" or "cargando" or "error" or "lleno";

    private async Task<System.Data.Common.DbConnection> OpenWithConfigAdminContextAsync(
        Guid tenantId,
        CancellationToken ct)
    {
        var conn = db.Database.GetDbConnection();
        if (conn.State != System.Data.ConnectionState.Open)
        {
            await conn.OpenAsync(ct);
        }

        await TenantDbContext.SetConfigAdminContextAsync(conn, tenantId, ct);
        return conn;
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
