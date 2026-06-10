using Flit.Modules.IdentityVerification.Domain;
using Flit.Modules.IdentityVerification.Ports;
using Flit.SharedKernel;

namespace Flit.Modules.IdentityVerification.Application;

/// <summary>
/// HU #9486 — Gate 100% Approved, cross-matching y webhook al motor de trámites.
/// </summary>
public static class ProcedureIdentityGate
{
    public sealed record ParticipantStatus(
        string ParticipantRole,
        Guid? VerificationSessionId,
        string Verdict);

    public sealed record GateStatusResponse(
        Guid ProcedureInstanceId,
        int RequiredParticipantCount,
        int ApprovedCount,
        bool IsBlocked,
        bool CanAdvance,
        IReadOnlyList<ParticipantStatus> Participants);

    public sealed record WebhookResponse(
        Guid ProcedureInstanceId,
        int ValidationsUpdated,
        bool Unblocked,
        bool AdvanceNotified);

    public abstract record GateError(string Code, string Message, int HttpStatus)
    {
        public sealed record InstanceNotFound()
            : GateError("INSTANCE_NOT_FOUND", "Instancia de trámite sin participantes IDSecure", 404);
    }

    public static async Task<Result<GateStatusResponse, GateError>> GetStatusAsync(
        Guid tenantId,
        Guid procedureInstanceId,
        IIdentityVerificationActivationRepository activationRepo,
        IVerificationSessionRepository sessionRepo,
        CancellationToken ct = default)
    {
        var invitations = await activationRepo.GetInvitationsByInstanceAsync(
            tenantId, procedureInstanceId, ct);
        if (invitations.Count == 0)
            return Result<GateStatusResponse, GateError>.Failure(new GateError.InstanceNotFound());

        var participants = new List<ParticipantStatus>();
        var approvedCount = 0;

        foreach (var invitation in invitations)
        {
            var session = await sessionRepo.FindByInvitationIdAsync(
                tenantId, invitation.Id, ct);
            var verdict = ResolveParticipantVerdict(session);
            participants.Add(new ParticipantStatus(
                invitation.ParticipantRole,
                session?.Id,
                verdict));

            if (verdict == IdentityValidationVerdict.Approved)
                approvedCount++;
        }

        var required = invitations.Count;
        var canAdvance = approvedCount == required && required > 0;
        var isBlocked = !canAdvance;

        return Result<GateStatusResponse, GateError>.Success(
            new GateStatusResponse(
                procedureInstanceId,
                required,
                approvedCount,
                isBlocked,
                canAdvance,
                participants));
    }

    public static async Task<Result<WebhookResponse, GateError>> ProcessIdentityStatusWebhookAsync(
        Guid tenantId,
        Guid procedureInstanceId,
        IIdentityVerificationActivationRepository activationRepo,
        IVerificationSessionRepository sessionRepo,
        IVerificationInvitationRepository invitationRepo,
        IProcedureIdentityValidationRepository validationRepo,
        IProcedureIdentityAdvanceNotifier advanceNotifier,
        IClock clock,
        CancellationToken ct = default)
    {
        var gateResult = await GetStatusAsync(
            tenantId,
            procedureInstanceId,
            activationRepo,
            sessionRepo,
            ct);
        if (!gateResult.IsSuccess)
            return Result<WebhookResponse, GateError>.Failure(gateResult.Error);

        var gate = gateResult.Value;
        var invitations = await activationRepo.GetInvitationsByInstanceAsync(
            tenantId, procedureInstanceId, ct);
        var now = clock.UtcNow;
        var updated = 0;

        foreach (var invitation in invitations)
        {
            var session = await sessionRepo.FindByInvitationIdAsync(
                tenantId, invitation.Id, ct);
            if (session is null)
                continue;

            var participantVerdict = ResolveParticipantVerdict(session);
            var mappedVerdict = MapToValidationVerdict(participantVerdict);

            var existing = await validationRepo.FindBySessionIdAsync(
                tenantId, session.Id, ct);
            if (existing is null)
            {
                var link = ProcedureIdentityValidation.LinkSession(
                    session,
                    invitation,
                    mappedVerdict,
                    now);
                await validationRepo.AddAsync(link, ct);
            }
            else
            {
                existing.UpdateVerdict(mappedVerdict, now, session.CreatedBy);
                await validationRepo.UpdateAsync(existing, ct);
            }

            updated++;
        }

        await validationRepo.SaveChangesAsync(ct);

        var unblocked = gate.CanAdvance;
        if (unblocked)
        {
            await advanceNotifier.NotifyUnblockedAsync(
                new ProcedureIdentityUnblockedNotification(
                    tenantId,
                    procedureInstanceId,
                    gate.ApprovedCount,
                    now),
                ct);
        }

        return Result<WebhookResponse, GateError>.Success(
            new WebhookResponse(procedureInstanceId, updated, unblocked, unblocked));
    }

    public static async Task SyncAfterSessionVerdictAsync(
        Guid tenantId,
        Guid verificationSessionId,
        IVerificationSessionRepository sessionRepo,
        IVerificationInvitationRepository invitationRepo,
        IVerificationOcrResultRepository ocrRepo,
        IVerificationAiVerdictRepository verdictRepo,
        IProcedureIdentityValidationRepository validationRepo,
        IIdSecureCrossMatchEvaluator crossMatchEvaluator,
        IIdentityVerificationActivationRepository activationRepo,
        IProcedureIdentityAdvanceNotifier advanceNotifier,
        IClock clock,
        CancellationToken ct = default)
    {
        var session = await sessionRepo.GetByIdForPublicFlowAsync(verificationSessionId, ct);
        if (session?.ProcedureInstanceId is null || session.VerificationInvitationId is null)
            return;

        var invitation = await invitationRepo.GetByIdAsync(
            tenantId, session.VerificationInvitationId.Value, ct);
        if (invitation is null)
            return;

        var aiVerdict = await verdictRepo.FindBySessionIdAsync(tenantId, session.Id, ct);
        if (aiVerdict is not null)
        {
            var ocr = await ocrRepo.FindBySessionIdAsync(tenantId, session.Id, ct);
            var ocrDoc = ExtractDocumentNumber(ocr?.ExtractedFieldsJson);
            var crossMatch = crossMatchEvaluator.Evaluate(ocrDoc, session.SubjectDocumentNumber);
            aiVerdict.ApplyCrossMatch(crossMatch.Passed);
            await verdictRepo.UpdateAsync(aiVerdict, ct);
        }

        var participantVerdict = ResolveParticipantVerdict(session);
        var mappedVerdict = MapToValidationVerdict(participantVerdict);
        var now = clock.UtcNow;

        var existing = await validationRepo.FindBySessionIdAsync(tenantId, session.Id, ct);
        if (existing is null)
        {
            await validationRepo.AddAsync(
                ProcedureIdentityValidation.LinkSession(session, invitation, mappedVerdict, now),
                ct);
        }
        else
        {
            existing.UpdateVerdict(mappedVerdict, now, session.CreatedBy);
            await validationRepo.UpdateAsync(existing, ct);
        }

        await validationRepo.SaveChangesAsync(ct);

        if (session.ProcedureInstanceId is Guid instanceId)
        {
            await ProcessIdentityStatusWebhookAsync(
                tenantId,
                instanceId,
                activationRepo,
                sessionRepo,
                invitationRepo,
                validationRepo,
                advanceNotifier,
                clock,
                ct);
        }
    }

    private static string ResolveParticipantVerdict(VerificationSession? session)
    {
        if (session is null)
            return IdentityValidationVerdict.Pending;

        if (session.Status == SessionStatus.Failed)
            return IdentityValidationVerdict.Rejected;

        if (session.Status == SessionStatus.Passed)
            return IdentityValidationVerdict.Approved;

        return IdentityValidationVerdict.Pending;
    }

    private static string MapToValidationVerdict(string participantVerdict) => participantVerdict;

    internal static string? ExtractDocumentNumber(string? extractedFieldsJson)
    {
        if (string.IsNullOrWhiteSpace(extractedFieldsJson))
            return null;

        const string key = $"\"{OcrFieldKeys.DocumentNumber}\":\"";
        var start = extractedFieldsJson.IndexOf(key, StringComparison.Ordinal);
        if (start < 0)
            return null;

        start += key.Length;
        var end = extractedFieldsJson.IndexOf('"', start);
        if (end < 0)
            return null;

        return extractedFieldsJson[start..end];
    }
}
