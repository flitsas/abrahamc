using System.Globalization;
using Flit.Modules.IdentityVerification.Domain;
using Flit.Modules.IdentityVerification.Ports;
using Flit.SharedKernel;

using static Flit.Modules.IdentityVerification.Domain.BiometricThresholds;

namespace Flit.Modules.IdentityVerification.Application;

/// <summary>
/// HU #9485 — Match facial, liveness y dictamen JSON → verification_ai_verdicts.
/// </summary>
public static class EvaluateBiometricVerdict
{
    public sealed record Command(
        Guid VerificationSessionId,
        string SelfieBase64,
        string DocumentFrontBase64,
        string? LivenessChallengeBase64);

    public sealed record Response(
        Guid VerdictId,
        Guid VerificationSessionId,
        string Verdict,
        decimal? MatchScore,
        bool? LivenessPassed,
        IReadOnlyDictionary<string, string> Dictamen);

    public abstract record EvaluateBiometricError(string Code, string Message, int HttpStatus)
    {
        public sealed record SessionNotFound()
            : EvaluateBiometricError("SESSION_NOT_FOUND", "Sesión no encontrada", 404);

        public sealed record SessionNotPending()
            : EvaluateBiometricError("SESSION_NOT_PENDING", "La sesión no admite evaluación biométrica", 409);

        public sealed record ImagesRequired()
            : EvaluateBiometricError("IMAGES_REQUIRED", "Se requieren selfie y foto frontal del documento", 400);
    }

    public static async Task<Result<Response, EvaluateBiometricError>> HandleAsync(
        Command cmd,
        IVerificationSessionRepository sessionRepo,
        IVerificationAiVerdictRepository verdictRepo,
        IIdSecureBiometricEvaluator biometricEvaluator,
        IClock clock,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(cmd.SelfieBase64)
            || string.IsNullOrWhiteSpace(cmd.DocumentFrontBase64))
            return Result<Response, EvaluateBiometricError>.Failure(new EvaluateBiometricError.ImagesRequired());

        var session = await sessionRepo.GetByIdForPublicFlowAsync(cmd.VerificationSessionId, ct);
        if (session is null)
            return Result<Response, EvaluateBiometricError>.Failure(new EvaluateBiometricError.SessionNotFound());

        if (session.Status != SessionStatus.Pending)
            return Result<Response, EvaluateBiometricError>.Failure(new EvaluateBiometricError.SessionNotPending());

        var evaluation = await biometricEvaluator.EvaluateAsync(
            new BiometricEvaluationRequest(
                cmd.SelfieBase64.Trim(),
                cmd.DocumentFrontBase64.Trim(),
                cmd.LivenessChallengeBase64?.Trim()),
            ct);

        var now = clock.UtcNow;
        var failureReasonsJson = BuildFailureReasonsJson(evaluation.FailureReasons);
        var dictamenWithTimestamp = AppendEvaluatedAt(evaluation.DictamenJson, now);

        var approved = evaluation.LivenessPassed
            && evaluation.MatchScore >= ApprovalMatchScore;

        VerificationAiVerdict persisted;
        if (approved)
        {
            persisted = VerificationAiVerdict.CreateApproved(
                session,
                evaluation.MatchScore,
                evaluation.LivenessPassed,
                dictamenWithTimestamp,
                now);
            session.MarkPassed(now, session.CreatedBy);
        }
        else
        {
            persisted = VerificationAiVerdict.CreateRejected(
                session,
                evaluation.MatchScore,
                evaluation.LivenessPassed,
                failureReasonsJson,
                dictamenWithTimestamp,
                now);
            session.MarkFailed(now, session.CreatedBy);
        }

        await verdictRepo.AddAsync(persisted, ct);
        await sessionRepo.UpdateAsync(session, ct);
        await verdictRepo.SaveChangesAsync(ct);

        return Result<Response, EvaluateBiometricError>.Success(
            new Response(
                persisted.Id,
                session.Id,
                persisted.Verdict,
                persisted.BiometricScore,
                persisted.LivenessPassed,
                ParseDictamen(dictamenWithTimestamp)));
    }

    /// <summary>Encadena gate + cross-match tras dictamen (HU #9486).</summary>
    public static Task AfterVerdictPersistedAsync(
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
        CancellationToken ct) =>
        ProcedureIdentityGate.SyncAfterSessionVerdictAsync(
            tenantId,
            verificationSessionId,
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

    internal static string BuildFailureReasonsJson(IReadOnlyList<BiometricFailureReason> reasons)
    {
        if (reasons.Count == 0)
            return "[]";

        var items = reasons.Select(r =>
            $$"""{"code":"{{EscapeJson(r.Code)}}","message":"{{EscapeJson(r.Message)}}"}""");
        return "[" + string.Join(",", items) + "]";
    }

    private static string AppendEvaluatedAt(string dictamenJson, DateTimeOffset now)
    {
        var evaluatedAt = now.ToString("O", CultureInfo.InvariantCulture);
        if (dictamenJson.EndsWith('}'))
            return dictamenJson[..^1] + $$""","{{BiometricDictamenKeys.EvaluatedAt}}":"{{evaluatedAt}}"}""";

        return dictamenJson;
    }

    private static Dictionary<string, string> ParseDictamen(string dictamenJson)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        if (dictamenJson.Length < 2 || dictamenJson[0] != '{')
            return result;

        foreach (var segment in dictamenJson.Trim('{', '}').Split(',', StringSplitOptions.RemoveEmptyEntries))
        {
            var colon = segment.IndexOf(':', StringComparison.Ordinal);
            if (colon <= 0)
                continue;

            var key = segment[..colon].Trim().Trim('"');
            var value = segment[(colon + 1)..].Trim().Trim('"');
            result[key] = value;
        }

        return result;
    }

    private static string EscapeJson(string value) =>
        value.Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal);
}
