using Flit.Api.Auth;
using Flit.Modules.ProceduresConfig.Application;
using Flit.Modules.ProceduresConfig.Ports;

namespace Flit.Api.Endpoints;

/// <summary>Administración de parametrización de trámites (#9409, #9692) — PostgreSQL.</summary>
public static class ProceduresConfigAdminEndpoints
{
    private const string ReadPermission = "modulo.parametrizacion.crud-total";
    private const string ManagePermission = "modulo.parametrizacion.gestionar";
    public sealed record CreateCatalogFamilyRequest(string Code, string Name, int? DisplayOrder);

    public sealed record PatchEdgeRequest(bool IsActive);

    public sealed record PatchTenantActivationRequest(bool IsActive);

    public sealed record PatchGlobalActiveRequest(bool IsActive);

    public sealed record PatchProcedureTypeRequest(string? Name, int? MaxSteps);

    public sealed record CreateEdgeInput(
        string EdgeCode,
        bool IsActive,
        bool IsRequired,
        int DisplayOrder,
        string? RoleLabel);

    public sealed record CreateProcedureTypeRequest(
        Guid TenantId,
        Guid? TrafficAgencyId,
        string FamilyCode,
        string Code,
        string Slug,
        string Name,
        int MaxSteps,
        IReadOnlyList<CreateEdgeInput> Edges);

    public sealed record CreateCatalogDocumentTypeRequest(
        string Code,
        string Name,
        string? DefaultPersonKind,
        int? DisplayOrder);

    public sealed record PatchCatalogDocumentTypeRequest(
        string? Name,
        int? DisplayOrder,
        bool? IsActive);

    public sealed record CreateRequiredDocumentRequest(
        string? EdgeCode,
        string DocumentTypeCode,
        string Kind,
        bool IsRequired,
        int DisplayOrder,
        string? ActorRole);

    public sealed record CreateQueryConfigRequest(
        string EdgeCode,
        string ConnectorCode,
        bool IsMandatory,
        bool IsOmitible,
        string PersonKindFilter,
        int DisplayOrder);

    public sealed record PatchQueryConfigRequest(
        bool? IsMandatory,
        bool? IsOmitible,
        string? PersonKindFilter,
        int? DisplayOrder);

    public sealed record PatchRequiredDocumentRequest(
        string? EdgeCode,
        string? Kind,
        bool? IsRequired,
        int? DisplayOrder,
        string? ActorRole,
        bool? IsActive);

    public static void MapProceduresConfigAdminEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/procedures-config/admin")
            .WithTags("Procedures Config - Admin");

        group.MapGet("/catalog/families", async (
            IProceduresConfigAdminRepository repo,
            CancellationToken ct) =>
        {
            var items = await ListAdminCatalogFamilies.HandleAsync(repo, ct);
            return Results.Ok(new { items });
        })
        .RequireTramitesPermission(ReadPermission)
        .WithName("ListAdminCatalogFamilies");

        group.MapPost("/catalog/families", async (
            CreateCatalogFamilyRequest req,
            IProceduresConfigAdminRepository repo,
            CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(req.Code) || string.IsNullOrWhiteSpace(req.Name))
            {
                return Results.BadRequest(new { error = "code y name son obligatorios." });
            }

            var (ok, error) = await CreateAdminCatalogFamily.HandleAsync(
                new CreateCatalogFamilyCommand(
                    req.Code.Trim().ToUpperInvariant(),
                    req.Name.Trim(),
                    req.DisplayOrder ?? 100),
                repo,
                ct);

            if (error is not null)
            {
                return error.Contains("Ya existe", StringComparison.OrdinalIgnoreCase)
                    ? Results.Conflict(new { error })
                    : Results.BadRequest(new { error });
            }

            return Results.Created($"/api/v1/procedures-config/admin/catalog/families/{ok!.Code}", ok);
        })
        .RequireTramitesPermission(ManagePermission)
        .WithName("CreateAdminCatalogFamily");

        group.MapDelete("/catalog/families/{code}", async (
            string code,
            IProceduresConfigAdminRepository repo,
            CancellationToken ct) =>
        {
            var (ok, error) = await DeleteAdminCatalogFamily.HandleAsync(
                code.Trim().ToUpperInvariant(),
                repo,
                ct);

            if (error is not null)
            {
                return error.Kind switch
                {
                    DeleteCatalogFamilyErrorKind.FamilyInUse => Results.Conflict(new
                    {
                        error = "FAMILY_IN_USE",
                        message = error.Message,
                    }),
                    _ => Results.NotFound(new { error = error.Message }),
                };
            }

            return ok ? Results.NoContent() : Results.NotFound(new { error = "Familia no encontrada." });
        })
        .RequireTramitesPermission(ManagePermission)
        .WithName("DeleteAdminCatalogFamily");

        group.MapGet("/catalog/edges", async (
            IProceduresConfigAdminRepository repo,
            CancellationToken ct) =>
        {
            var items = await ListAdminCatalogEdges.HandleAsync(repo, ct);
            return Results.Ok(new { items });
        })
        .RequireTramitesPermission(ReadPermission)
        .WithName("ListAdminCatalogEdges");

        group.MapGet("/catalog/document-types", async (
            IProceduresConfigAdminRepository repo,
            CancellationToken ct) =>
        {
            var items = await ListAdminCatalogDocumentTypes.HandleAsync(repo, ct);
            return Results.Ok(new { items });
        })
        .RequireTramitesPermission(ReadPermission)
        .WithName("ListAdminCatalogDocumentTypes");

        group.MapPost("/catalog/document-types", async (
            CreateCatalogDocumentTypeRequest req,
            Guid tenantId,
            IProceduresConfigAdminRepository repo,
            CancellationToken ct) =>
        {
            if (tenantId == Guid.Empty)
            {
                return Results.BadRequest(new { error = "tenantId es requerido." });
            }

            if (string.IsNullOrWhiteSpace(req.Code) || string.IsNullOrWhiteSpace(req.Name))
            {
                return Results.BadRequest(new { error = "code y name son obligatorios." });
            }

            var (ok, error) = await CreateAdminCatalogDocumentType.HandleAsync(
                new CreateCatalogDocumentTypeCommand(
                    tenantId,
                    req.Code.Trim().ToUpperInvariant(),
                    req.Name.Trim(),
                    req.DefaultPersonKind,
                    req.DisplayOrder ?? 100),
                repo,
                ct);

            if (error is not null)
            {
                return error.Contains("ya existe", StringComparison.OrdinalIgnoreCase)
                    ? Results.Conflict(new { error })
                    : Results.BadRequest(new { error });
            }

            return Results.Created($"/api/v1/procedures-config/admin/catalog/document-types/{ok!.Code}", ok);
        })
        .RequireTramitesPermission(ManagePermission)
        .WithName("CreateAdminCatalogDocumentType");

        group.MapPatch("/catalog/document-types/{code}", async (
            string code,
            PatchCatalogDocumentTypeRequest req,
            Guid tenantId,
            IProceduresConfigAdminRepository repo,
            CancellationToken ct) =>
        {
            if (tenantId == Guid.Empty)
            {
                return Results.BadRequest(new { error = "tenantId es requerido." });
            }

            var (ok, error) = await UpdateAdminCatalogDocumentType.HandleAsync(
                new UpdateCatalogDocumentTypeCommand(
                    tenantId,
                    code.Trim().ToUpperInvariant(),
                    req.Name?.Trim(),
                    req.DisplayOrder,
                    req.IsActive),
                repo,
                ct);

            return error is not null
                ? Results.BadRequest(new { error })
                : Results.Ok(ok);
        })
        .RequireTramitesPermission(ManagePermission)
        .WithName("UpdateAdminCatalogDocumentType");

        group.MapDelete("/catalog/document-types/{code}", async (
            string code,
            Guid tenantId,
            IProceduresConfigAdminRepository repo,
            CancellationToken ct) =>
        {
            if (tenantId == Guid.Empty)
            {
                return Results.BadRequest(new { error = "tenantId es requerido." });
            }

            var ok = await DeactivateAdminCatalogDocumentType.HandleAsync(
                tenantId,
                code.Trim().ToUpperInvariant(),
                repo,
                ct);

            return ok
                ? Results.NoContent()
                : Results.NotFound(new { error = "Documento no encontrado en catálogo." });
        })
        .RequireTramitesPermission(ManagePermission)
        .WithName("DeactivateAdminCatalogDocumentType");

        group.MapGet("/types", async (
            Guid tenantId,
            Guid? trafficAgencyId,
            IProceduresConfigAdminRepository repo,
            CancellationToken ct) =>
        {
            if (tenantId == Guid.Empty)
            {
                return Results.BadRequest(new { error = "tenantId es requerido." });
            }

            var items = await ListAdminProcedureTypes.HandleAsync(
                new ListAdminProcedureTypes.Query(tenantId, trafficAgencyId),
                repo,
                ct);

            return Results.Ok(new { items });
        })
        .RequireTramitesPermission(ReadPermission)
        .WithName("ListAdminProcedureTypes");

        group.MapPost("/types", async (
            CreateProcedureTypeRequest req,
            IProceduresConfigAdminRepository repo,
            CancellationToken ct) =>
        {
            if (req.TenantId == Guid.Empty)
            {
                return Results.BadRequest(new { error = "tenantId es requerido." });
            }

            var edges = req.Edges.Select(e => new CreateProcedureTypeEdgeInput(
                e.EdgeCode,
                e.IsActive,
                e.IsRequired,
                e.DisplayOrder,
                e.RoleLabel)).ToList();

            var (ok, error) = await CreateAdminProcedureType.HandleAsync(
                new CreateProcedureTypeCommand(
                    req.TenantId,
                    req.TrafficAgencyId,
                    req.FamilyCode,
                    req.Code,
                    req.Slug,
                    req.Name,
                    req.MaxSteps,
                    edges),
                repo,
                ct);

            if (error is not null)
            {
                return error.Kind switch
                {
                    CreateProcedureTypeErrorKind.Conflict => Results.Conflict(new { error = error.Message }),
                    CreateProcedureTypeErrorKind.NotFound => Results.NotFound(new { error = error.Message }),
                    _ => Results.BadRequest(new { error = error.Message }),
                };
            }

            return Results.Created($"/api/v1/procedures-config/admin/types/{ok!.Code}/matrix", ok);
        })
        .RequireTramitesPermission(ManagePermission)
        .WithName("CreateAdminProcedureType");

        group.MapGet("/types/{code}/matrix", async (
            string code,
            Guid tenantId,
            Guid? trafficAgencyId,
            IProceduresConfigAdminRepository repo,
            CancellationToken ct) =>
        {
            if (tenantId == Guid.Empty)
            {
                return Results.BadRequest(new { error = "tenantId es requerido." });
            }

            var matrix = await GetAdminProcedureMatrix.HandleAsync(
                new GetAdminProcedureMatrix.Query(tenantId, code, trafficAgencyId),
                repo,
                ct);

            return matrix is null
                ? Results.NotFound(new { error = $"Tipo '{code}' no encontrado." })
                : Results.Ok(matrix);
        })
        .RequireTramitesPermission(ReadPermission)
        .WithName("GetAdminProcedureMatrix");

        group.MapPatch("/types/{code}/tenant-activation", async (
            string code,
            PatchTenantActivationRequest req,
            Guid tenantId,
            Guid? trafficAgencyId,
            IProceduresConfigAdminRepository repo,
            CancellationToken ct) =>
        {
            if (tenantId == Guid.Empty)
            {
                return Results.BadRequest(new { error = "tenantId es requerido." });
            }

            var ok = await UpdateAdminTenantActivation.HandleAsync(
                new UpdateAdminTenantActivation.Command(tenantId, code, trafficAgencyId, req.IsActive),
                repo,
                ct);

            return ok
                ? Results.Ok(new { code, tenantId, trafficAgencyId, isActive = req.IsActive })
                : Results.NotFound(new { error = $"Tipo '{code}' no encontrado." });
        })
        .RequireTramitesPermission(ManagePermission)
        .WithName("PatchAdminProcedureTenantActivation");

        group.MapPatch("/types/{code}/global-active", async (
            string code,
            PatchGlobalActiveRequest req,
            Guid tenantId,
            IProceduresConfigAdminRepository repo,
            CancellationToken ct) =>
        {
            if (tenantId == Guid.Empty)
            {
                return Results.BadRequest(new { error = "tenantId es requerido." });
            }

            var ok = await UpdateAdminProcedureTypeGlobal.HandleAsync(
                new UpdateAdminProcedureTypeGlobal.Command(tenantId, code, req.IsActive),
                repo,
                ct);

            return ok
                ? Results.Ok(new { code, isActive = req.IsActive })
                : Results.NotFound(new { error = $"Tipo '{code}' no encontrado." });
        })
        .RequireTramitesPermission(ManagePermission)
        .WithName("PatchAdminProcedureGlobalActive");

        group.MapPatch("/types/{code}", async (
            string code,
            PatchProcedureTypeRequest req,
            Guid tenantId,
            IProceduresConfigAdminRepository repo,
            CancellationToken ct) =>
        {
            if (tenantId == Guid.Empty)
            {
                return Results.BadRequest(new { error = "tenantId es requerido." });
            }

            if (string.IsNullOrWhiteSpace(req.Name) && req.MaxSteps is null)
            {
                return Results.BadRequest(new { error = "Indique name y/o maxSteps." });
            }

            if (req.MaxSteps is < 1 or > 4)
            {
                return Results.BadRequest(new { error = "maxSteps debe estar entre 1 y 4." });
            }

            var ok = await UpdateAdminProcedureTypeMetadata.HandleAsync(
                new UpdateAdminProcedureTypeMetadata.Command(tenantId, code, req.Name, req.MaxSteps),
                repo,
                ct);

            return ok
                ? Results.Ok(new { code, name = req.Name, maxSteps = req.MaxSteps })
                : Results.NotFound(new { error = $"Tipo '{code}' no encontrado." });
        })
        .RequireTramitesPermission(ManagePermission)
        .WithName("PatchAdminProcedureType");

        group.MapPatch("/types/{code}/matrix/edges/{edgeCode}", async (
            string code,
            string edgeCode,
            PatchEdgeRequest req,
            Guid tenantId,
            IProceduresConfigAdminRepository repo,
            CancellationToken ct) =>
        {
            if (tenantId == Guid.Empty)
            {
                return Results.BadRequest(new { error = "tenantId es requerido." });
            }

            var ok = await UpdateAdminProcedureEdge.HandleAsync(
                new UpdateAdminProcedureEdge.Command(tenantId, code, edgeCode, req.IsActive),
                repo,
                ct);

            return ok
                ? Results.Ok(new { code, edgeCode, isActive = req.IsActive })
                : Results.NotFound(new { error = $"Tipo '{code}' o arista '{edgeCode}' no encontrada." });
        })
        .RequireTramitesPermission(ManagePermission)
        .WithName("PatchAdminProcedureEdge");

        group.MapPost("/types/{code}/required-documents", async (
            string code,
            CreateRequiredDocumentRequest req,
            Guid tenantId,
            IProceduresConfigAdminRepository repo,
            CancellationToken ct) =>
        {
            if (tenantId == Guid.Empty)
            {
                return Results.BadRequest(new { error = "tenantId es requerido." });
            }

            if (string.IsNullOrWhiteSpace(req.DocumentTypeCode))
            {
                return Results.BadRequest(new { error = "documentTypeCode es obligatorio." });
            }

            if (string.IsNullOrWhiteSpace(req.ActorRole))
            {
                return Results.BadRequest(new { error = "actorRole es obligatorio (propietario, comprador, locatario, vehiculo)." });
            }

            var edgeCode = string.IsNullOrWhiteSpace(req.EdgeCode) ? "documentos" : req.EdgeCode.Trim();

            var (ok, error) = await CreateAdminRequiredDocument.HandleAsync(
                new CreateRequiredDocumentCommand(
                    tenantId,
                    code,
                    edgeCode,
                    req.DocumentTypeCode.Trim(),
                    req.Kind,
                    req.IsRequired,
                    req.DisplayOrder,
                    req.ActorRole),
                repo,
                ct);

            return error is not null
                ? Results.BadRequest(new { error })
                : Results.Created($"/api/v1/procedures-config/admin/types/{code}/required-documents/{ok!.Id}", ok);
        })
        .RequireTramitesPermission(ManagePermission)
        .WithName("CreateAdminRequiredDocument");

        group.MapPatch("/types/{code}/required-documents/{documentId:guid}", async (
            string code,
            Guid documentId,
            PatchRequiredDocumentRequest req,
            Guid tenantId,
            IProceduresConfigAdminRepository repo,
            CancellationToken ct) =>
        {
            if (tenantId == Guid.Empty)
            {
                return Results.BadRequest(new { error = "tenantId es requerido." });
            }

            var (ok, error) = await UpdateAdminRequiredDocument.HandleAsync(
                new UpdateRequiredDocumentCommand(
                    tenantId,
                    code,
                    documentId,
                    req.EdgeCode,
                    req.Kind,
                    req.IsRequired,
                    req.DisplayOrder,
                    req.ActorRole,
                    req.IsActive),
                repo,
                ct);

            return error is not null
                ? Results.BadRequest(new { error })
                : Results.Ok(ok);
        })
        .RequireTramitesPermission(ManagePermission)
        .WithName("PatchAdminRequiredDocument");

        group.MapDelete("/types/{code}/required-documents/{documentId:guid}", async (
            string code,
            Guid documentId,
            Guid tenantId,
            IProceduresConfigAdminRepository repo,
            CancellationToken ct) =>
        {
            if (tenantId == Guid.Empty)
            {
                return Results.BadRequest(new { error = "tenantId es requerido." });
            }

            var ok = await DeactivateAdminRequiredDocument.HandleAsync(
                new DeactivateAdminRequiredDocument.Command(tenantId, code, documentId),
                repo,
                ct);

            return ok
                ? Results.NoContent()
                : Results.NotFound(new { error = "Documento no encontrado." });
        })
        .RequireTramitesPermission(ManagePermission)
        .WithName("DeleteAdminRequiredDocument");

        group.MapGet("/catalog/query-connectors", async (
            IProceduresConfigAdminRepository repo,
            CancellationToken ct) =>
        {
            var items = await ListAdminCatalogQueryConnectors.HandleAsync(repo, ct);
            return Results.Ok(new { items });
        })
        .RequireTramitesPermission(ReadPermission)
        .WithName("ListAdminCatalogQueryConnectors");

        group.MapGet("/types/{code}/query-configs", async (
            string code,
            Guid tenantId,
            IProceduresConfigAdminRepository repo,
            CancellationToken ct) =>
        {
            if (tenantId == Guid.Empty)
            {
                return Results.BadRequest(new { error = "tenantId es requerido." });
            }

            var items = await ListAdminQueryConfigs.HandleAsync(
                new ListAdminQueryConfigs.Query(tenantId, code),
                repo,
                ct);

            return Results.Ok(new { items });
        })
        .RequireTramitesPermission(ReadPermission)
        .WithName("ListAdminQueryConfigs");

        group.MapPost("/types/{code}/query-configs", async (
            string code,
            CreateQueryConfigRequest req,
            Guid tenantId,
            IProceduresConfigAdminRepository repo,
            CancellationToken ct) =>
        {
            if (tenantId == Guid.Empty)
            {
                return Results.BadRequest(new { error = "tenantId es requerido." });
            }

            if (string.IsNullOrWhiteSpace(req.EdgeCode) || string.IsNullOrWhiteSpace(req.ConnectorCode))
            {
                return Results.BadRequest(new { error = "edgeCode y connectorCode son obligatorios." });
            }

            var (ok, error) = await CreateAdminQueryConfig.HandleAsync(
                new CreateQueryConfigCommand(
                    tenantId,
                    code,
                    req.EdgeCode.Trim(),
                    req.ConnectorCode.Trim().ToUpperInvariant(),
                    req.IsMandatory,
                    req.IsOmitible,
                    req.PersonKindFilter ?? "any",
                    req.DisplayOrder),
                repo,
                ct);

            return error is not null
                ? Results.BadRequest(new { error })
                : Results.Created($"/api/v1/procedures-config/admin/types/{code}/query-configs/{ok!.Id}", ok);
        })
        .RequireTramitesPermission(ManagePermission)
        .WithName("CreateAdminQueryConfig");

        group.MapPatch("/types/{code}/query-configs/{configId:guid}", async (
            string code,
            Guid configId,
            PatchQueryConfigRequest req,
            Guid tenantId,
            IProceduresConfigAdminRepository repo,
            CancellationToken ct) =>
        {
            if (tenantId == Guid.Empty)
            {
                return Results.BadRequest(new { error = "tenantId es requerido." });
            }

            var (ok, error) = await UpdateAdminQueryConfig.HandleAsync(
                new UpdateQueryConfigCommand(
                    tenantId,
                    code,
                    configId,
                    req.IsMandatory,
                    req.IsOmitible,
                    req.PersonKindFilter,
                    req.DisplayOrder),
                repo,
                ct);

            return error is not null
                ? Results.BadRequest(new { error })
                : Results.Ok(ok);
        })
        .RequireTramitesPermission(ManagePermission)
        .WithName("PatchAdminQueryConfig");

        group.MapDelete("/types/{code}/query-configs/{configId:guid}", async (
            string code,
            Guid configId,
            Guid tenantId,
            IProceduresConfigAdminRepository repo,
            CancellationToken ct) =>
        {
            if (tenantId == Guid.Empty)
            {
                return Results.BadRequest(new { error = "tenantId es requerido." });
            }

            var ok = await DeleteAdminQueryConfig.HandleAsync(
                new DeleteAdminQueryConfig.Command(tenantId, code, configId),
                repo,
                ct);

            return ok
                ? Results.NoContent()
                : Results.NotFound(new { error = "Configuración de consulta no encontrada." });
        })
        .RequireTramitesPermission(ManagePermission)
        .WithName("DeleteAdminQueryConfig");
    }
}
