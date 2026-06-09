using System.Text.Json;
using Flit.Modules.ProceduresConfig.Application;
using Flit.Modules.ProceduresConfig.Ports;
using Flit.SharedKernel.Pdf;

namespace Flit.Api.Endpoints;

/// <summary>HU #9442 DOC-01 — Plantillas versionadas y generación documental QuestPDF.</summary>
public static class DocumentTemplatesEndpoints
{
    public sealed record CreateTemplateRequest(
        string Code,
        string Name,
        Guid DocumentTypeId,
        string Scope,
        Guid? TenantId,
        Guid? ActorUserId = null);

    public sealed record AddVersionRequest(
        string BodyInline,
        JsonElement MarkerMap,
        Guid? ActorUserId = null);

    public sealed record UpdateMarkerMapRequest(JsonElement MarkerMap, Guid? ActorUserId = null);

    public sealed record GenerateDocumentRequest(
        Guid TenantId,
        Guid TemplateVersionId,
        Guid DocumentTypeId,
        JsonElement DataContext,
        Guid? ActorUserId = null);

    public sealed record PackageConsolidatedRequest(
        Guid TenantId,
        Guid? ActorUserId = null);

    public static void MapDocumentTemplatesEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/procedures-config/document-templates")
            .WithTags("Procedures Config - Document Templates");

        group.MapPost("/", async (
            CreateTemplateRequest req,
            IDocumentTemplateRepository repo,
            CancellationToken ct) =>
        {
            var actor = req.ActorUserId ?? ProceduresConfigEndpoints.DefaultActorUserId;
            var result = await CreateDocumentTemplate.HandleAsync(
                new CreateDocumentTemplate.Command(
                    req.Code, req.Name, req.DocumentTypeId, req.Scope, req.TenantId, actor),
                repo,
                ct);

            return result.Match(
                ok => Results.Created($"/api/v1/procedures-config/document-templates/{ok.Id}", ok),
                err => MapError(err));
        });

        group.MapGet("/", async (IDocumentTemplateRepository repo, CancellationToken ct) =>
            Results.Ok(await repo.ListAsync(ct)));

        group.MapPost("/{templateId:guid}/versions", async (
            Guid templateId,
            AddVersionRequest req,
            IDocumentTemplateRepository repo,
            CancellationToken ct) =>
        {
            var actor = req.ActorUserId ?? ProceduresConfigEndpoints.DefaultActorUserId;
            var result = await AddDocumentTemplateVersion.HandleAsync(
                new AddDocumentTemplateVersion.Command(templateId, req.BodyInline, req.MarkerMap, actor),
                repo,
                ct);

            return result.Match(
                ok => Results.Created(
                    $"/api/v1/procedures-config/document-templates/{templateId}/versions/{ok.Id}",
                    ok),
                err => MapError(err));
        });

        group.MapPost("/versions/{versionId:guid}/publish", async (
            Guid versionId,
            IDocumentTemplateRepository repo,
            CancellationToken ct) =>
        {
            var result = await PublishDocumentTemplateVersion.HandleAsync(
                new PublishDocumentTemplateVersion.Command(
                    versionId,
                    ProceduresConfigEndpoints.DefaultActorUserId),
                repo,
                ct);

            return result.Match(Results.Ok, err => MapError(err));
        });

        group.MapPatch("/versions/{versionId:guid}/marker-map", async (
            Guid versionId,
            UpdateMarkerMapRequest req,
            IDocumentTemplateRepository repo,
            CancellationToken ct) =>
        {
            var actor = req.ActorUserId ?? ProceduresConfigEndpoints.DefaultActorUserId;
            var result = await UpdateDocumentTemplateVersionMarkerMap.HandleAsync(
                new UpdateDocumentTemplateVersionMarkerMap.Command(versionId, req.MarkerMap, actor),
                repo,
                ct);

            return result.Match(Results.Ok, err => MapError(err));
        });

        app.MapPost("/api/v1/procedures/instances/{instanceId:guid}/documents/generate", async (
            Guid instanceId,
            GenerateDocumentRequest req,
            IProcedureInstanceExistsPort instances,
            IDocumentTemplateRepository templates,
            IMarkerPdfRenderer pdfRenderer,
            IProcedurePdfFileStore fileStore,
            IProcedureDocumentRepository documents,
            CancellationToken ct) =>
        {
            var actor = req.ActorUserId ?? ProceduresConfigEndpoints.DefaultActorUserId;
            var result = await GenerateProcedureDocument.HandleAsync(
                new GenerateProcedureDocument.Command(
                    req.TenantId,
                    instanceId,
                    req.TemplateVersionId,
                    req.DocumentTypeId,
                    req.DataContext,
                    actor),
                instances,
                templates,
                pdfRenderer,
                fileStore,
                documents,
                ct);

            return result.Match(Results.Ok, err => MapError(err));
        })
        .WithName("GenerateProcedureDocument")
        .WithTags("Procedures - Documents");

        app.MapPost("/api/v1/procedures/instances/{instanceId:guid}/documents/package-consolidated", async (
            Guid instanceId,
            PackageConsolidatedRequest req,
            IProcedureInstanceTrafficAgencyPort instanceLookup,
            IOtConsolidatedDocOrderRepository consolidatedOrders,
            IProcedureDocumentRepository documents,
            IProcedurePdfFileStore fileStore,
            IApprovedIdentityEvidencePort identityEvidence,
            IConsolidatedPdfPackager packager,
            CancellationToken ct) =>
        {
            var actor = req.ActorUserId ?? ProceduresConfigEndpoints.DefaultActorUserId;
            var result = await PackageConsolidatedDocuments.HandleAsync(
                new PackageConsolidatedDocuments.Command(req.TenantId, instanceId, actor),
                instanceLookup,
                consolidatedOrders,
                documents,
                fileStore,
                identityEvidence,
                packager,
                ct);

            return result.Match(Results.Ok, err => MapError(err));
        })
        .WithName("PackageConsolidatedDocuments")
        .WithTags("Procedures - Documents")
        .WithSummary("HU #9443 DOC-02 — Empaqueta consolidado según orden OT + evidencia identidad");
    }

    private static IResult MapError(DocumentTemplateError err) => err.Kind switch
    {
        DocumentTemplateErrorKind.NotFound => Results.NotFound(new { error = err.Message }),
        DocumentTemplateErrorKind.ImmutableVersion => Results.Conflict(new { error = err.Message }),
        DocumentTemplateErrorKind.Conflict => Results.Conflict(new { error = err.Message }),
        _ => Results.BadRequest(new { error = err.Message }),
    };
}
