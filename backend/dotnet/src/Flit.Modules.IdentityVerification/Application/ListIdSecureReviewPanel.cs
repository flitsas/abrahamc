using Flit.Modules.IdentityVerification.Domain;
using Flit.Modules.IdentityVerification.Ports;
using Flit.SharedKernel;

namespace Flit.Modules.IdentityVerification.Application;

/// <summary>
/// HU #9487 — Panel backoffice Enviadas / Aprobadas / Rechazadas.
/// </summary>
public static class ListIdSecureReviewPanel
{
    public sealed record Query(Guid TenantId, Guid? ProcedureTypeId);

    public sealed record PanelItem(
        Guid VerificationSessionId,
        Guid ProcedureInstanceId,
        Guid? ProcedureTypeId,
        string ParticipantRole,
        string RecipientEmail,
        string Bucket,
        string CurrentVerdict,
        DateTimeOffset UpdatedAt);

    public sealed record Response(
        IReadOnlyList<PanelItem> Sent,
        IReadOnlyList<PanelItem> Approved,
        IReadOnlyList<PanelItem> Rejected);

    public abstract record PanelError(string Code, string Message, int HttpStatus)
    {
        public sealed record Forbidden()
            : PanelError("FORBIDDEN", "Permiso idsecure.review requerido", 403);
    }

    public static async Task<Result<Response, PanelError>> HandleAsync(
        Query query,
        bool hasReviewPermission,
        IIdSecureReviewReadRepository readRepo,
        IVerificationAiVerdictRepository verdictRepo,
        IVerificationManualOverrideRepository overrideRepo,
        CancellationToken ct = default)
    {
        if (!hasReviewPermission)
            return Result<Response, PanelError>.Failure(new PanelError.Forbidden());

        var sessions = await readRepo.ListSessionsByTenantAsync(query.TenantId, ct);
        var sent = new List<PanelItem>();
        var approved = new List<PanelItem>();
        var rejected = new List<PanelItem>();

        foreach (var session in sessions)
        {
            if (session.VerificationInvitationId is null)
                continue;

            var invitation = await readRepo.FindInvitationByIdAsync(
                query.TenantId, session.VerificationInvitationId.Value, ct);
            if (invitation is null)
                continue;

            Guid? procedureTypeId = null;
            if (session.ProcedureInstanceId is Guid instanceId)
            {
                var domainEvent = await readRepo.FindDomainEventByInstanceAsync(
                    query.TenantId, instanceId, ct);
                procedureTypeId = ExtractProcedureTypeId(domainEvent?.PayloadJson);
            }

            if (query.ProcedureTypeId is Guid filterType
                && procedureTypeId != filterType)
                continue;

            var latestOverride = await overrideRepo.FindLatestBySessionIdAsync(
                query.TenantId, session.Id, ct);
            var aiVerdict = await verdictRepo.FindBySessionIdAsync(
                query.TenantId, session.Id, ct);

            var currentVerdict = ResolveCurrentVerdict(session, aiVerdict, latestOverride);
            var bucket = ClassifyBucket(currentVerdict, session);

            var item = new PanelItem(
                session.Id,
                session.ProcedureInstanceId ?? Guid.Empty,
                procedureTypeId,
                invitation.ParticipantRole,
                invitation.RecipientEmail,
                bucket,
                currentVerdict,
                session.UpdatedAt);

            switch (bucket)
            {
                case ReviewPanelBuckets.Approved:
                    approved.Add(item);
                    break;
                case ReviewPanelBuckets.Rejected:
                    rejected.Add(item);
                    break;
                default:
                    sent.Add(item);
                    break;
            }
        }

        return Result<Response, PanelError>.Success(
            new Response(sent, approved, rejected));
    }

    internal static string ResolveCurrentVerdict(
        VerificationSession session,
        VerificationAiVerdict? aiVerdict,
        VerificationManualOverride? latestOverride)
    {
        if (latestOverride is not null)
            return latestOverride.NewVerdict;

        if (aiVerdict is not null)
            return aiVerdict.Verdict;

        if (session.Status == SessionStatus.Passed)
            return AiVerdictValues.Approved;

        if (session.Status == SessionStatus.Failed)
            return AiVerdictValues.Rejected;

        return IdentityValidationVerdict.Pending;
    }

    internal static string ClassifyBucket(string currentVerdict, VerificationSession session)
    {
        if (currentVerdict is AiVerdictValues.Approved or ManualOverrideVerdict.Approved
            or IdentityValidationVerdict.Approved)
            return ReviewPanelBuckets.Approved;

        if (currentVerdict is AiVerdictValues.Rejected or ManualOverrideVerdict.Rejected
            or IdentityValidationVerdict.Rejected)
            return ReviewPanelBuckets.Rejected;

        return ReviewPanelBuckets.Sent;
    }

    internal static Guid? ExtractProcedureTypeId(string? payloadJson)
    {
        if (string.IsNullOrWhiteSpace(payloadJson))
            return null;

        const string key = "\"procedureTypeId\":\"";
        var start = payloadJson.IndexOf(key, StringComparison.Ordinal);
        if (start < 0)
            return null;

        start += key.Length;
        var end = payloadJson.IndexOf('"', start);
        if (end < 0)
            return null;

        return Guid.TryParse(payloadJson[start..end], out var id) ? id : null;
    }
}
