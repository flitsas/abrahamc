using Flit.Modules.IdentityVerification.Domain;
using Flit.Modules.IdentityVerification.Ports;
using Flit.SharedKernel;

namespace Flit.Modules.IdentityVerification.Application;

/// <summary>
/// HU #9487 — Override manual auditado (RF-4.7).
/// </summary>
public static class ApplyManualVerdictOverride
{
    public sealed record Command(
        Guid TenantId,
        Guid VerificationSessionId,
        string NewVerdict,
        string Reason,
        Guid OperatorUserId);

    public sealed record Response(
        Guid OverrideId,
        Guid VerificationSessionId,
        string PreviousVerdict,
        string NewVerdict);

    public abstract record OverrideError(string Code, string Message, int HttpStatus)
    {
        public sealed record Forbidden()
            : OverrideError("FORBIDDEN", "Permiso idsecure.review requerido", 403);

        public sealed record SessionNotFound()
            : OverrideError("SESSION_NOT_FOUND", "Sesión no encontrada", 404);

        public sealed record InvalidVerdict()
            : OverrideError("INVALID_VERDICT", "new_verdict debe ser approved o rejected", 400);

        public sealed record ReasonRequired()
            : OverrideError("REASON_REQUIRED", "El motivo del override es obligatorio", 400);
    }

    public static async Task<Result<Response, OverrideError>> HandleAsync(
        Command cmd,
        bool hasReviewPermission,
        IVerificationSessionRepository sessionRepo,
        IVerificationAiVerdictRepository verdictRepo,
        IVerificationManualOverrideRepository overrideRepo,
        IProcedureIdentityValidationRepository validationRepo,
        IIdentityVerificationAuditRepository auditRepo,
        IClock clock,
        CancellationToken ct = default)
    {
        if (!hasReviewPermission)
            return Result<Response, OverrideError>.Failure(new OverrideError.Forbidden());

        if (string.IsNullOrWhiteSpace(cmd.Reason))
            return Result<Response, OverrideError>.Failure(new OverrideError.ReasonRequired());

        if (cmd.NewVerdict is not (ManualOverrideVerdict.Approved or ManualOverrideVerdict.Rejected))
            return Result<Response, OverrideError>.Failure(new OverrideError.InvalidVerdict());

        var session = await sessionRepo.GetByIdAsync(cmd.TenantId, cmd.VerificationSessionId, ct);
        if (session is null)
            return Result<Response, OverrideError>.Failure(new OverrideError.SessionNotFound());

        var aiVerdict = await verdictRepo.FindBySessionIdAsync(cmd.TenantId, session.Id, ct);
        var latestOverride = await overrideRepo.FindLatestBySessionIdAsync(
            cmd.TenantId, session.Id, ct);
        var previousVerdict = ListIdSecureReviewPanel.ResolveCurrentVerdict(
            session, aiVerdict, latestOverride);

        var now = clock.UtcNow;
        var manualOverride = VerificationManualOverride.Create(
            session,
            previousVerdict,
            cmd.NewVerdict,
            cmd.Reason,
            cmd.OperatorUserId,
            now);

        if (cmd.NewVerdict == ManualOverrideVerdict.Approved)
            session.MarkPassed(now, cmd.OperatorUserId);
        else
            session.MarkFailed(now, cmd.OperatorUserId);

        await overrideRepo.AddAsync(manualOverride, ct);
        await sessionRepo.UpdateAsync(session, ct);

        var validation = await validationRepo.FindBySessionIdAsync(
            cmd.TenantId, session.Id, ct);
        if (validation is not null)
        {
            var mapped = cmd.NewVerdict == ManualOverrideVerdict.Approved
                ? IdentityValidationVerdict.Approved
                : IdentityValidationVerdict.Rejected;
            validation.UpdateVerdict(mapped, now, cmd.OperatorUserId);
            await validationRepo.UpdateAsync(validation, ct);
        }

        var auditJson =
            $$"""{"previous_verdict":"{{previousVerdict}}","new_verdict":"{{cmd.NewVerdict}}","reason":"{{EscapeJson(cmd.Reason.Trim())}}"}""";

        await auditRepo.AppendAsync(new IdentityVerificationAuditEntry
        {
            Id = Guid.CreateVersion7(),
            TenantId = cmd.TenantId,
            TableName = "verification_manual_overrides",
            RecordId = manualOverride.Id,
            Operation = 'I',
            ChangedBy = cmd.OperatorUserId,
            ChangedAt = now,
            NewValuesJson = auditJson,
        }, ct);

        await overrideRepo.SaveChangesAsync(ct);

        return Result<Response, OverrideError>.Success(
            new Response(
                manualOverride.Id,
                session.Id,
                previousVerdict,
                cmd.NewVerdict));
    }

    private static string EscapeJson(string value) =>
        value.Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal);
}
