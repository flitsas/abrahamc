using Flit.Modules.IdentityVerification.Domain;
using Flit.Modules.IdentityVerification.Ports;
using Flit.SharedKernel;

namespace Flit.Modules.IdentityVerification.Application;

/// <summary>
/// HU #9483 — Completa un paso del stepper con gating secuencial y evidencias (cámara + firma).
/// </summary>
public static class CompleteVerificationSessionStep
{
    public const string InvalidSequenceMessage =
        "Secuencia inválida: debe completar los pasos anteriores antes de continuar.";

    public sealed record Command(
        Guid VerificationSessionId,
        short StepNumber,
        string? ImageBase64,
        string? DocumentFrontBase64,
        string? DocumentBackBase64,
        string? SignaturePngBase64);

    public sealed record Response(
        Guid VerificationSessionId,
        short CurrentStep,
        Guid? EvidenceId,
        Guid? FileId);

    public abstract record CompleteStepError(string Code, string Message, int HttpStatus)
    {
        public sealed record SessionNotFound()
            : CompleteStepError("SESSION_NOT_FOUND", "Sesión no encontrada", 404);

        public sealed record InvalidSequence()
            : CompleteStepError("INVALID_STEP_SEQUENCE", InvalidSequenceMessage, 422);

        public sealed record ImagesRequired(string Detail)
            : CompleteStepError("IMAGES_REQUIRED", Detail, 400);

        public sealed record InvalidImagePayload()
            : CompleteStepError("INVALID_IMAGE", "La imagen capturada no es un JPEG válido.", 400);

        public sealed record SignatureRequired()
            : CompleteStepError("SIGNATURE_REQUIRED", "La firma canvas es obligatoria en el paso 4.", 400);

        public sealed record InvalidSignaturePayload()
            : CompleteStepError("INVALID_SIGNATURE", "La firma canvas no es una imagen PNG válida.", 400);
    }

    public static async Task<Result<Response, CompleteStepError>> HandleAsync(
        Command cmd,
        IVerificationSessionRepository sessionRepo,
        IVerificationSessionStepRepository stepRepo,
        IVerificationEvidenceRepository evidenceRepo,
        IIdSecureFileRepository fileRepo,
        IIdSecureBlobStorage? blobStorage,
        IClock clock,
        CancellationToken ct = default)
    {
        if (cmd.StepNumber is < 1 or > 4)
            return Result<Response, CompleteStepError>.Failure(new CompleteStepError.InvalidSequence());

        var session = await sessionRepo.GetByIdForPublicFlowAsync(cmd.VerificationSessionId, ct);
        if (session is null)
            return Result<Response, CompleteStepError>.Failure(new CompleteStepError.SessionNotFound());

        var steps = await stepRepo.GetBySessionIdAsync(session.TenantId, session.Id, ct);
        if (steps.Count == 0)
            return Result<Response, CompleteStepError>.Failure(new CompleteStepError.InvalidSequence());

        if (!CanCompleteStep(session.CurrentStep, cmd.StepNumber, steps))
            return Result<Response, CompleteStepError>.Failure(new CompleteStepError.InvalidSequence());

        var targetStep = steps.First(s => s.StepNumber == cmd.StepNumber);
        var now = clock.UtcNow;
        Guid? evidenceId = null;
        Guid? fileId = null;

        switch (cmd.StepNumber)
        {
            case 1:
                {
                    if (string.IsNullOrWhiteSpace(cmd.DocumentFrontBase64)
                        || string.IsNullOrWhiteSpace(cmd.DocumentBackBase64))
                    {
                        return Result<Response, CompleteStepError>.Failure(
                            new CompleteStepError.ImagesRequired(
                                "Debe capturar el anverso y reverso del documento en el paso 1."));
                    }

                    if (!TryDecodeJpeg(cmd.DocumentFrontBase64, out var frontBytes)
                        || !TryDecodeJpeg(cmd.DocumentBackBase64, out var backBytes))
                    {
                        return Result<Response, CompleteStepError>.Failure(
                            new CompleteStepError.InvalidImagePayload());
                    }

                    var frontResult = await PersistEvidenceAsync(
                        session,
                        IdSecureStoredFile.CreateDocumentFront,
                        VerificationEvidence.CreateDocumentFront,
                        frontBytes,
                        fileRepo,
                        evidenceRepo,
                        blobStorage,
                        now,
                        ct);

                    await PersistEvidenceAsync(
                        session,
                        IdSecureStoredFile.CreateDocumentBack,
                        VerificationEvidence.CreateDocumentBack,
                        backBytes,
                        fileRepo,
                        evidenceRepo,
                        blobStorage,
                        now,
                        ct);

                    evidenceId = frontResult.EvidenceId;
                    fileId = frontResult.FileId;
                    break;
                }
            case 2:
                {
                    if (string.IsNullOrWhiteSpace(cmd.ImageBase64))
                    {
                        return Result<Response, CompleteStepError>.Failure(
                            new CompleteStepError.ImagesRequired(
                                "Debe capturar una selfie en el paso 2."));
                    }

                    if (!TryDecodeJpeg(cmd.ImageBase64, out var selfieBytes))
                    {
                        return Result<Response, CompleteStepError>.Failure(
                            new CompleteStepError.InvalidImagePayload());
                    }

                    var selfieResult = await PersistEvidenceAsync(
                        session,
                        IdSecureStoredFile.CreateSelfie,
                        VerificationEvidence.CreateSelfie,
                        selfieBytes,
                        fileRepo,
                        evidenceRepo,
                        blobStorage,
                        now,
                        ct);

                    evidenceId = selfieResult.EvidenceId;
                    fileId = selfieResult.FileId;
                    break;
                }
            case 3:
                {
                    if (string.IsNullOrWhiteSpace(cmd.ImageBase64))
                    {
                        return Result<Response, CompleteStepError>.Failure(
                            new CompleteStepError.ImagesRequired(
                                "Debe capturar la prueba de vida en el paso 3."));
                    }

                    if (!TryDecodeJpeg(cmd.ImageBase64, out var livenessBytes))
                    {
                        return Result<Response, CompleteStepError>.Failure(
                            new CompleteStepError.InvalidImagePayload());
                    }

                    var livenessResult = await PersistEvidenceAsync(
                        session,
                        IdSecureStoredFile.CreateLiveness,
                        VerificationEvidence.CreateLiveness,
                        livenessBytes,
                        fileRepo,
                        evidenceRepo,
                        blobStorage,
                        now,
                        ct);

                    evidenceId = livenessResult.EvidenceId;
                    fileId = livenessResult.FileId;
                    break;
                }
            case 4:
                {
                    if (string.IsNullOrWhiteSpace(cmd.SignaturePngBase64))
                    {
                        return Result<Response, CompleteStepError>.Failure(
                            new CompleteStepError.SignatureRequired());
                    }

                    if (!TryDecodePng(cmd.SignaturePngBase64, out var pngBytes) || pngBytes.Length == 0)
                    {
                        return Result<Response, CompleteStepError>.Failure(
                            new CompleteStepError.InvalidSignaturePayload());
                    }

                    var signatureResult = await PersistEvidenceAsync(
                        session,
                        IdSecureStoredFile.CreateSignatureCanvas,
                        VerificationEvidence.CreateSignatureCanvas,
                        pngBytes,
                        fileRepo,
                        evidenceRepo,
                        blobStorage,
                        now,
                        ct);

                    evidenceId = signatureResult.EvidenceId;
                    fileId = signatureResult.FileId;
                    break;
                }
        }

        targetStep.MarkCompleted(now, session.CreatedBy);
        session.AdvanceAfterStepCompleted(cmd.StepNumber, now, session.CreatedBy);

        await stepRepo.UpdateAsync(targetStep, ct);
        await sessionRepo.UpdateAsync(session, ct);
        await stepRepo.SaveChangesAsync(ct);

        return Result<Response, CompleteStepError>.Success(
            new Response(session.Id, session.CurrentStep, evidenceId, fileId));
    }

    internal static bool CanCompleteStep(
        short currentStep,
        short stepNumber,
        IReadOnlyList<VerificationSessionStep> steps)
    {
        if (stepNumber != currentStep + 1)
            return false;

        for (short n = 1; n < stepNumber; n++)
        {
            var previous = steps.FirstOrDefault(s => s.StepNumber == n);
            if (previous is null || previous.Status != StepStatus.Completed)
                return false;
        }

        return true;
    }

    private static async Task<(Guid EvidenceId, Guid FileId)> PersistEvidenceAsync(
        VerificationSession session,
        Func<Guid, Guid, ReadOnlySpan<byte>, Guid, DateTimeOffset, IdSecureStoredFile> createFile,
        Func<VerificationSession, Guid, DateTimeOffset, VerificationEvidence> createEvidence,
        byte[] bytes,
        IIdSecureFileRepository fileRepo,
        IVerificationEvidenceRepository evidenceRepo,
        IIdSecureBlobStorage? blobStorage,
        DateTimeOffset now,
        CancellationToken ct)
    {
        var storedFile = createFile(session.TenantId, session.Id, bytes, session.CreatedBy, now);

        if (blobStorage is not null)
        {
            await blobStorage.PutEncryptedAsync(
                storedFile.Bucket,
                storedFile.ObjectKey,
                bytes,
                storedFile.ContentType,
                ct);
        }

        await fileRepo.AddAsync(storedFile, ct);

        var evidence = createEvidence(session, storedFile.Id, now);
        await evidenceRepo.AddAsync(evidence, ct);

        return (evidence.Id, storedFile.Id);
    }

    private static bool TryDecodeJpeg(string base64, out byte[] bytes)
    {
        bytes = [];
        try
        {
            bytes = Convert.FromBase64String(base64.Trim());
            return bytes.Length >= 2
                   && bytes[0] == 0xFF
                   && bytes[1] == 0xD8;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static bool TryDecodePng(string base64, out byte[] bytes)
    {
        bytes = [];
        try
        {
            bytes = Convert.FromBase64String(base64.Trim());
            return bytes.Length >= 8
                   && bytes[0] == 0x89
                   && bytes[1] == 0x50
                   && bytes[2] == 0x4E
                   && bytes[3] == 0x47;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
