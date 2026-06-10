using Flit.Modules.IdentityVerification.Application;
using Flit.Modules.IdentityVerification.Ports;
using Flit.Modules.Notifications.Ports;
using Flit.SharedKernel;

namespace Flit.Api.Endpoints;

/// <summary>
/// Endpoints publicos IDSecure (sin auth corporativa — HU #9480/#9481).
/// </summary>
public static class IdSecurePublicEndpoints
{
    public sealed record OpenInvitationResponse(
        Guid InvitationId,
        Guid ProcedureInstanceId,
        DateTimeOffset ExpiresAt);

    public sealed record PublicSessionResponse(
        Guid VerificationSessionId,
        short CurrentStep,
        Guid InvitationId);

    public sealed record CompleteStepRequest(
        string? ImageBase64,
        string? DocumentFrontBase64,
        string? DocumentBackBase64,
        string? SignaturePngBase64);

    public sealed record CompleteStepResponse(
        Guid VerificationSessionId,
        short CurrentStep,
        Guid? EvidenceId,
        Guid? FileId);

    public sealed record ProcessOcrRequest(
        string DocumentFrontBase64,
        string DocumentBackBase64);

    public sealed record ProcessOcrResponse(
        Guid OcrResultId,
        Guid VerificationSessionId,
        IReadOnlyDictionary<string, string> ExtractedFields);

    public sealed record EvaluateBiometricRequest(
        string SelfieBase64,
        string DocumentFrontBase64,
        string? LivenessChallengeBase64);

    public sealed record EvaluateBiometricResponse(
        Guid VerdictId,
        Guid VerificationSessionId,
        string Verdict,
        decimal? MatchScore,
        bool? LivenessPassed,
        IReadOnlyDictionary<string, string> Dictamen);

    public static void MapIdSecurePublicEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/public/idsecure").WithTags("IDSecure - Public");

        group.MapGet("/session", async (
            string token,
            IVerificationInvitationRepository invitationRepo,
            IVerificationSessionRepository sessionRepo,
            IVerificationSessionStepRepository stepRepo,
            IIdSecureCatalogLookup catalogLookup,
            IInvitationTokenHasher hasher,
            IClock clock,
            CancellationToken ct) =>
        {
            var result = await BootstrapPublicVerificationSession.HandleAsync(
                new BootstrapPublicVerificationSession.Command(token),
                invitationRepo,
                sessionRepo,
                stepRepo,
                catalogLookup,
                hasher,
                clock,
                ct);

            return result.Match<IResult>(
                onSuccess: r => Results.Ok(new PublicSessionResponse(
                    r.VerificationSessionId, r.CurrentStep, r.InvitationId)),
                onFailure: err => Results.Problem(
                    detail: err.Message,
                    statusCode: err.HttpStatus,
                    title: err.Code));
        })
        .WithName("IdSecurePublicSession");

        group.MapPost("/sessions/{sessionId:guid}/steps/{stepNumber:int}/complete", async (
            Guid sessionId,
            int stepNumber,
            CompleteStepRequest? body,
            IVerificationSessionRepository sessionRepo,
            IVerificationSessionStepRepository stepRepo,
            IVerificationEvidenceRepository evidenceRepo,
            IIdSecureFileRepository fileRepo,
            IIdSecureBlobStorage blobStorage,
            IClock clock,
            CancellationToken ct) =>
        {
            var result = await CompleteVerificationSessionStep.HandleAsync(
                new CompleteVerificationSessionStep.Command(
                    sessionId,
                    (short)stepNumber,
                    body?.ImageBase64,
                    body?.DocumentFrontBase64,
                    body?.DocumentBackBase64,
                    body?.SignaturePngBase64),
                sessionRepo,
                stepRepo,
                evidenceRepo,
                fileRepo,
                blobStorage,
                clock,
                ct);

            return result.Match<IResult>(
                onSuccess: r => Results.Ok(new CompleteStepResponse(
                    r.VerificationSessionId, r.CurrentStep, r.EvidenceId, r.FileId)),
                onFailure: err => Results.Problem(
                    detail: err.Message,
                    statusCode: err.HttpStatus,
                    title: err.Code));
        })
        .WithName("IdSecureCompleteStep");

        group.MapPost("/sessions/{sessionId:guid}/ocr/process", async (
            Guid sessionId,
            ProcessOcrRequest body,
            IVerificationSessionRepository sessionRepo,
            IVerificationSessionStepRepository stepRepo,
            IVerificationOcrResultRepository ocrRepo,
            IIdSecureDocumentOcrProvider ocrProvider,
            IClock clock,
            CancellationToken ct) =>
        {
            var result = await ProcessDocumentOcr.HandleAsync(
                new ProcessDocumentOcr.Command(
                    sessionId,
                    body.DocumentFrontBase64,
                    body.DocumentBackBase64),
                sessionRepo,
                stepRepo,
                ocrRepo,
                ocrProvider,
                clock,
                ct);

            return result.Match<IResult>(
                onSuccess: r => Results.Ok(new ProcessOcrResponse(
                    r.OcrResultId, r.VerificationSessionId, r.ExtractedFields)),
                onFailure: err => Results.Problem(
                    detail: err.Message,
                    statusCode: err.HttpStatus,
                    title: err.Code));
        })
        .WithName("IdSecureProcessDocumentOcr");

        group.MapPost("/sessions/{sessionId:guid}/biometric/evaluate", async (
            Guid sessionId,
            EvaluateBiometricRequest body,
            IVerificationSessionRepository sessionRepo,
            IVerificationInvitationRepository invitationRepo,
            IVerificationOcrResultRepository ocrRepo,
            IVerificationAiVerdictRepository verdictRepo,
            IProcedureIdentityValidationRepository validationRepo,
            IIdentityVerificationActivationRepository activationRepo,
            IIdSecureBiometricEvaluator biometricEvaluator,
            IIdSecureCrossMatchEvaluator crossMatchEvaluator,
            IProcedureIdentityAdvanceNotifier advanceNotifier,
            INotificationsPublisher notificationsPublisher,
            IClock clock,
            CancellationToken ct) =>
        {
            var result = await EvaluateBiometricVerdict.HandleAsync(
                new EvaluateBiometricVerdict.Command(
                    sessionId,
                    body.SelfieBase64,
                    body.DocumentFrontBase64,
                    body.LivenessChallengeBase64),
                sessionRepo,
                verdictRepo,
                biometricEvaluator,
                clock,
                ct);

            return await result.Match<Task<IResult>>(
                onSuccess: async r =>
                {
                    var session = await sessionRepo.GetByIdForPublicFlowAsync(sessionId, ct);
                    if (session is not null)
                    {
                        await EvaluateBiometricVerdict.AfterVerdictPersistedAsync(
                            session.TenantId,
                            sessionId,
                            sessionRepo,
                            invitationRepo,
                            ocrRepo,
                            verdictRepo,
                            validationRepo,
                            crossMatchEvaluator,
                            activationRepo,
                            advanceNotifier,
                            clock,
                            ct);

                        if (session.ProcedureInstanceId is Guid instanceId)
                        {
                            await IdentityGateNotificationHelper.NotifyIfPossibleAsync(
                                session.TenantId,
                                instanceId,
                                activationRepo,
                                sessionRepo,
                                notificationsPublisher,
                                clock,
                                ct);
                        }
                    }

                    return Results.Ok(new EvaluateBiometricResponse(
                        r.VerdictId,
                        r.VerificationSessionId,
                        r.Verdict,
                        r.MatchScore,
                        r.LivenessPassed,
                        r.Dictamen));
                },
                onFailure: err => Task.FromResult<IResult>(Results.Problem(
                    detail: err.Message,
                    statusCode: err.HttpStatus,
                    title: err.Code)));
        })
        .WithName("IdSecureEvaluateBiometric");

        group.MapGet("/open", async (
            string token,
            IVerificationInvitationRepository repo,
            IInvitationTokenHasher hasher,
            IClock clock,
            CancellationToken ct) =>
        {
            var result = await OpenInvitationToken.HandleAsync(
                new OpenInvitationToken.Command(token),
                repo,
                hasher,
                clock,
                ct);

            return result.Match<IResult>(
                onSuccess: r => Results.Ok(new OpenInvitationResponse(
                    r.InvitationId, r.ProcedureInstanceId, r.ExpiresAt)),
                onFailure: err => Results.Problem(
                    detail: err.Message,
                    statusCode: err.HttpStatus,
                    title: err.Code));
        })
        .WithName("IdSecureOpenInvitation");
    }
}
