using System.Text.Json;
using Flit.Modules.ProceduresConfig.Domain;
using Flit.Modules.ProceduresConfig.Ports;
using Flit.SharedKernel;
using Flit.SharedKernel.Pdf;

namespace Flit.Modules.ProceduresConfig.Application;

/// <summary>HU #9442 DOC-01 — Genera PDF desde plantilla versionada y persiste en procedure_documents.</summary>
public static class GenerateProcedureDocument
{
    public sealed record Command(
        Guid TenantId,
        Guid ProcedureInstanceId,
        Guid TemplateVersionId,
        Guid DocumentTypeId,
        JsonElement DataContext,
        Guid ActorUserId);

    public sealed record ResponseDto(
        Guid ProcedureDocumentId,
        Guid FileId,
        Guid TemplateVersionId,
        IReadOnlyDictionary<string, string?> ResolvedMarkers);

    public static async Task<Result<ResponseDto, DocumentTemplateError>> HandleAsync(
        Command command,
        IProcedureInstanceExistsPort instances,
        IDocumentTemplateRepository templates,
        IMarkerPdfRenderer pdfRenderer,
        IProcedurePdfFileStore fileStore,
        IProcedureDocumentRepository documents,
        CancellationToken ct = default)
    {
        if (!await instances.ExistsAsync(command.TenantId, command.ProcedureInstanceId, ct))
        {
            return Result<ResponseDto, DocumentTemplateError>.Failure(
                new DocumentTemplateError(DocumentTemplateErrorKind.NotFound, "Instancia de trámite no encontrada."));
        }

        var version = await templates.GetVersionByIdAsync(command.TemplateVersionId, ct);
        if (version is null)
        {
            return Result<ResponseDto, DocumentTemplateError>.Failure(
                new DocumentTemplateError(DocumentTemplateErrorKind.NotFound, "Versión de plantilla no encontrada."));
        }

        if (version.PublishedAt is null)
        {
            return Result<ResponseDto, DocumentTemplateError>.Failure(
                new DocumentTemplateError(
                    DocumentTemplateErrorKind.Validation,
                    "Solo se pueden generar documentos desde versiones publicadas."));
        }

        var template = await templates.GetByIdAsync(version.TemplateId, ct);
        if (template is null)
        {
            return Result<ResponseDto, DocumentTemplateError>.Failure(
                new DocumentTemplateError(DocumentTemplateErrorKind.NotFound, "Plantilla no encontrada."));
        }

        var resolved = MarkerMapResolver.Resolve(version.MarkerMap, command.DataContext);
        var layout = TemplateBodyParser.Parse(version.BodyInline, template.Name);
        var pdfBytes = pdfRenderer.Render(new MarkerPdfDocumentModel(
            layout.Title,
            layout.Subtitle,
            resolved));

        var stored = await fileStore.StoreAsync(command.TenantId, pdfBytes, command.ActorUserId, ct);
        var snapshotJson = JsonSerializer.Serialize(resolved);

        var doc = await documents.AddGeneratedAsync(
            command.TenantId,
            command.ProcedureInstanceId,
            command.DocumentTypeId,
            version.Id,
            stored.FileId,
            snapshotJson,
            command.ActorUserId,
            ct);

        return Result<ResponseDto, DocumentTemplateError>.Success(new ResponseDto(
            doc.Id,
            stored.FileId,
            version.Id,
            resolved));
    }
}
