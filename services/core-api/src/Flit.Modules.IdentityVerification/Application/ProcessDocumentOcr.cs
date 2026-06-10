using Flit.Modules.IdentityVerification.Domain;
using Flit.Modules.IdentityVerification.Ports;
using Flit.SharedKernel;

namespace Flit.Modules.IdentityVerification.Application;

/// <summary>
/// HU #9484 — Pipeline OCR documento (Vertex/mock) → verification_ocr_results.
/// </summary>
public static class ProcessDocumentOcr
{
    public sealed record Command(
        Guid VerificationSessionId,
        string DocumentFrontBase64,
        string DocumentBackBase64);

    public sealed record Response(
        Guid OcrResultId,
        Guid VerificationSessionId,
        IReadOnlyDictionary<string, string> ExtractedFields);

    public abstract record ProcessOcrError(string Code, string Message, int HttpStatus)
    {
        public sealed record SessionNotFound()
            : ProcessOcrError("SESSION_NOT_FOUND", "Sesión no encontrada", 404);

        public sealed record SessionNotPending()
            : ProcessOcrError("SESSION_NOT_PENDING", "La sesión no admite procesamiento OCR", 409);

        public sealed record ImagesRequired()
            : ProcessOcrError("IMAGES_REQUIRED", "Se requieren imágenes front y back del documento", 400);

        public sealed record OcrFailed(string Reason)
            : ProcessOcrError("OCR_FAILED", Reason, 422);
    }

    public static async Task<Result<Response, ProcessOcrError>> HandleAsync(
        Command cmd,
        IVerificationSessionRepository sessionRepo,
        IVerificationSessionStepRepository stepRepo,
        IVerificationOcrResultRepository ocrRepo,
        IIdSecureDocumentOcrProvider ocrProvider,
        IClock clock,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(cmd.DocumentFrontBase64)
            || string.IsNullOrWhiteSpace(cmd.DocumentBackBase64))
            return Result<Response, ProcessOcrError>.Failure(new ProcessOcrError.ImagesRequired());

        var session = await sessionRepo.GetByIdForPublicFlowAsync(cmd.VerificationSessionId, ct);
        if (session is null)
            return Result<Response, ProcessOcrError>.Failure(new ProcessOcrError.SessionNotFound());

        if (session.Status != SessionStatus.Pending)
            return Result<Response, ProcessOcrError>.Failure(new ProcessOcrError.SessionNotPending());

        var steps = await stepRepo.GetBySessionIdAsync(session.TenantId, session.Id, ct);
        var documentStep = steps.FirstOrDefault(s => s.StepCode == StepCodes.DocumentCapture);

        var ocrResult = await ocrProvider.ExtractAsync(
            new DocumentOcrRequest(cmd.DocumentFrontBase64.Trim(), cmd.DocumentBackBase64.Trim()),
            ct);

        var now = clock.UtcNow;

        if (!ocrResult.Success || ocrResult.Fields is null)
        {
            var reason = ocrResult.FailureMessage ?? "Imagen ilegible o OCR no pudo extraer datos";
            session.MarkFailed(now, session.CreatedBy);
            documentStep?.MarkFailed(reason, now, session.CreatedBy);

            await sessionRepo.UpdateAsync(session, ct);
            if (documentStep is not null)
                await stepRepo.UpdateAsync(documentStep, ct);
            await sessionRepo.SaveChangesAsync(ct);

            return Result<Response, ProcessOcrError>.Failure(new ProcessOcrError.OcrFailed(reason));
        }

        var extracted = new Dictionary<string, string>
        {
            [OcrFieldKeys.FullName] = ocrResult.Fields.FullName,
            [OcrFieldKeys.DocumentNumber] = ocrResult.Fields.DocumentNumber,
            [OcrFieldKeys.IssueDate] = ocrResult.Fields.IssueDate,
        };

        var extractedJson =
            $$"""{"{{OcrFieldKeys.FullName}}":"{{ocrResult.Fields.FullName}}","{{OcrFieldKeys.DocumentNumber}}":"{{ocrResult.Fields.DocumentNumber}}","{{OcrFieldKeys.IssueDate}}":"{{ocrResult.Fields.IssueDate}}"}""";

        var confidenceJson = ocrResult.ConfidenceScores is null
            ? "{}"
            : "{" + string.Join(",", ocrResult.ConfidenceScores.Select(kv => $"\"{kv.Key}\":{kv.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)}")) + "}";

        var persisted = VerificationOcrResult.Create(
            session,
            extractedJson,
            confidenceJson,
            ocrResult.RawProviderResponseJson,
            now);

        session.ApplySuccessfulOcr(ocrResult.Fields.DocumentNumber, now, session.CreatedBy);

        await ocrRepo.AddAsync(persisted, ct);
        await sessionRepo.UpdateAsync(session, ct);
        await ocrRepo.SaveChangesAsync(ct);

        return Result<Response, ProcessOcrError>.Success(
            new Response(persisted.Id, session.Id, extracted));
    }
}
