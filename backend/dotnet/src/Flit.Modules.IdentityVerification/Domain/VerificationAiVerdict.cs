namespace Flit.Modules.IdentityVerification.Domain;

/// <summary>
/// Dictamen IA biométrico. Tabla: verification_ai_verdicts (HU #9485).
/// </summary>
public sealed class VerificationAiVerdict
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid VerificationSessionId { get; private set; }
    public string Verdict { get; private set; } = AiVerdictValues.Approved;
    public decimal? BiometricScore { get; private set; }
    public bool? LivenessPassed { get; private set; }
    public bool? CrossMatchPassed { get; private set; }
    public string FailureReasonsJson { get; private set; } = "[]";
    public string DictamenJson { get; private set; } = "{}";
    public DateTimeOffset DecidedAt { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public Guid CreatedBy { get; private set; }
    public int RowVersion { get; private set; } = 1;

    private VerificationAiVerdict() { }

    public static VerificationAiVerdict CreateApproved(
        VerificationSession session,
        decimal matchScore,
        bool livenessPassed,
        string dictamenJson,
        DateTimeOffset now)
    {
        return new VerificationAiVerdict
        {
            Id = Guid.CreateVersion7(),
            TenantId = session.TenantId,
            VerificationSessionId = session.Id,
            Verdict = AiVerdictValues.Approved,
            BiometricScore = matchScore,
            LivenessPassed = livenessPassed,
            CrossMatchPassed = null,
            FailureReasonsJson = "[]",
            DictamenJson = dictamenJson,
            DecidedAt = now,
            CreatedAt = now,
            CreatedBy = session.CreatedBy,
        };
    }

    public void ApplyCrossMatch(bool passed)
    {
        CrossMatchPassed = passed;
    }

    public static VerificationAiVerdict CreateRejected(
        VerificationSession session,
        decimal? matchScore,
        bool livenessPassed,
        string failureReasonsJson,
        string dictamenJson,
        DateTimeOffset now)
    {
        return new VerificationAiVerdict
        {
            Id = Guid.CreateVersion7(),
            TenantId = session.TenantId,
            VerificationSessionId = session.Id,
            Verdict = AiVerdictValues.Rejected,
            BiometricScore = matchScore,
            LivenessPassed = livenessPassed,
            CrossMatchPassed = null,
            FailureReasonsJson = failureReasonsJson,
            DictamenJson = dictamenJson,
            DecidedAt = now,
            CreatedAt = now,
            CreatedBy = session.CreatedBy,
        };
    }
}

public static class AiVerdictValues
{
    public const string Approved = "approved";
    public const string Rejected = "rejected";
    public const string ManualReview = "manual_review";
}

public static class BiometricFailureCodes
{
    public const string CfI10Liveness = "CF-I10";
}

public static class BiometricDictamenKeys
{
    public const string Provider = "provider";
    public const string MatchScore = "matchScore";
    public const string LivenessPassed = "livenessPassed";
    public const string Verdict = "verdict";
    public const string EvaluatedAt = "evaluatedAt";
}

public static class BiometricThresholds
{
    public const decimal ApprovalMatchScore = 0.8500m;
}
