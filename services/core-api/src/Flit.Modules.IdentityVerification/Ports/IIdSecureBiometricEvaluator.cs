namespace Flit.Modules.IdentityVerification.Ports;

/// <summary>
/// Evaluación match facial + liveness (Vertex AI en prod, mock en DEV — HU #9485).
/// </summary>
public interface IIdSecureBiometricEvaluator
{
    Task<BiometricEvaluationResult> EvaluateAsync(
        BiometricEvaluationRequest request,
        CancellationToken ct = default);
}

public sealed record BiometricEvaluationRequest(
    string SelfieBase64,
    string DocumentFrontBase64,
    string? LivenessChallengeBase64 = null);

public sealed record BiometricEvaluationResult(
    bool LivenessPassed,
    decimal MatchScore,
    IReadOnlyList<BiometricFailureReason> FailureReasons,
    string DictamenJson);

public sealed record BiometricFailureReason(string Code, string Message);
