using Flit.Modules.ProceduresConfig.Ports;
using Flit.SharedKernel;
using Flit.SharedKernel.Pdf;

namespace Flit.Modules.ProceduresConfig.Application;

/// <summary>HU #9443 DOC-02 — Empaqueta consolidado final según orden OT + evidencia identidad.</summary>
public static class PackageConsolidatedDocuments
{
    public sealed record Command(
        Guid TenantId,
        Guid ProcedureInstanceId,
        Guid ActorUserId);

    public sealed record ResponseDto(
        Guid ConsolidatedFileId,
        string Bucket,
        string ObjectKey,
        long SizeBytes,
        int DocumentCount,
        int IdentityEvidenceCount,
        IReadOnlyList<Guid> IncludedDocumentTypeIds);

    public static async Task<Result<ResponseDto, DocumentTemplateError>> HandleAsync(
        Command command,
        IProcedureInstanceTrafficAgencyPort instanceLookup,
        IOtConsolidatedDocOrderRepository consolidatedOrders,
        IProcedureDocumentRepository documents,
        IProcedurePdfFileStore fileStore,
        IApprovedIdentityEvidencePort identityEvidence,
        IConsolidatedPdfPackager packager,
        CancellationToken ct = default)
    {
        var trafficAgencyId = await instanceLookup.GetTrafficAgencyIdAsync(
            command.TenantId,
            command.ProcedureInstanceId,
            ct);

        if (!trafficAgencyId.HasValue)
        {
            return Result<ResponseDto, DocumentTemplateError>.Failure(
                new DocumentTemplateError(
                    DocumentTemplateErrorKind.NotFound,
                    "Instancia de trámite no encontrada o sin organismo de tránsito."));
        }

        var orderData = await consolidatedOrders.GetActiveOrderWithItemsAsync(
            trafficAgencyId.Value,
            ct);
        if (orderData is null)
        {
            return Result<ResponseDto, DocumentTemplateError>.Failure(
                new DocumentTemplateError(
                    DocumentTemplateErrorKind.NotFound,
                    "No hay orden consolidado activo para el organismo de tránsito."));
        }

        var (_, orderItems) = orderData.Value;
        var instanceDocs = await documents.ListByInstanceAsync(
            command.TenantId,
            command.ProcedureInstanceId,
            ct);

        var docsByType = instanceDocs
            .GroupBy(d => d.DocumentTypeId)
            .ToDictionary(g => g.Key, g => g.First());

        var orderedPdfs = new List<byte[]>();
        var includedTypeIds = new List<Guid>();

        foreach (var item in orderItems.Where(i => i.Source == "global" && i.DocumentTypeId.HasValue))
        {
            var typeId = item.DocumentTypeId!.Value;
            if (!docsByType.TryGetValue(typeId, out var doc))
                continue;

            var pdfBytes = await fileStore.TryGetAsync(doc.FileId, ct);
            if (pdfBytes is null || pdfBytes.Length == 0)
                continue;

            orderedPdfs.Add(pdfBytes);
            includedTypeIds.Add(typeId);
        }

        var evidenceBlobs = await identityEvidence.ListApprovedForInstanceAsync(
            command.TenantId,
            command.ProcedureInstanceId,
            ct);

        var identityImages = evidenceBlobs
            .Select(e => new ConsolidatedEvidenceImage(e.Label, e.ImageBytes))
            .ToList();

        if (orderedPdfs.Count == 0 && identityImages.Count == 0)
        {
            return Result<ResponseDto, DocumentTemplateError>.Failure(
                new DocumentTemplateError(
                    DocumentTemplateErrorKind.Validation,
                    "No hay documentos ni evidencia de identidad para empaquetar."));
        }

        var packageBytes = packager.Package(new ConsolidatedPackageInput(orderedPdfs, identityImages));
        var stored = await fileStore.StoreAsync(command.TenantId, packageBytes, command.ActorUserId, ct);

        return Result<ResponseDto, DocumentTemplateError>.Success(new ResponseDto(
            stored.FileId,
            stored.Bucket,
            stored.ObjectKey,
            stored.SizeBytes,
            orderedPdfs.Count,
            identityImages.Count,
            includedTypeIds));
    }
}
