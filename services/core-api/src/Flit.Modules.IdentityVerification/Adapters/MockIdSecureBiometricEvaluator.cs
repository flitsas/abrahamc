using System.Globalization;
using Flit.Modules.IdentityVerification.Domain;
using Flit.Modules.IdentityVerification.Ports;

namespace Flit.Modules.IdentityVerification.Adapters;

/// <summary>
/// Mock Vertex biométrico para DEV/tests (HU #9485).
/// Payload con prefijo LIVENESS_FAIL simula CF-I10.
/// </summary>
public sealed class MockIdSecureBiometricEvaluator : IIdSecureBiometricEvaluator
{
    public const string LivenessFailMarker = "LIVENESS_FAIL";
    public const decimal DefaultMatchScore = 0.9500m;

    public Task<BiometricEvaluationResult> EvaluateAsync(
        BiometricEvaluationRequest request,
        CancellationToken ct = default)
    {
        var livenessFailed =
            ContainsMarker(request.SelfieBase64)
            || ContainsMarker(request.LivenessChallengeBase64);

        var matchScore = livenessFailed ? 0.7200m : DefaultMatchScore;
        var livenessPassed = !livenessFailed;
        var approved = livenessPassed && matchScore >= BiometricThresholds.ApprovalMatchScore;

        var failureReasons = new List<BiometricFailureReason>();
        if (!livenessPassed)
        {
            failureReasons.Add(new BiometricFailureReason(
                BiometricFailureCodes.CfI10Liveness,
                "Prueba de vida no superada: posible spoofing o captura inválida"));
        }

        var verdict = approved ? AiVerdictValues.Approved : AiVerdictValues.Rejected;
        var dictamenJson = BuildDictamenJson(verdict, matchScore, livenessPassed);

        return Task.FromResult(new BiometricEvaluationResult(
            livenessPassed,
            matchScore,
            failureReasons,
            dictamenJson));
    }

    private static bool ContainsMarker(string? value) =>
        !string.IsNullOrWhiteSpace(value)
        && value.Contains(LivenessFailMarker, StringComparison.OrdinalIgnoreCase);

    private static string BuildDictamenJson(string verdict, decimal matchScore, bool livenessPassed)
    {
        var score = matchScore.ToString("0.0000", CultureInfo.InvariantCulture);
        var liveness = livenessPassed ? "true" : "false";
        return $$"""{"provider":"mock_vertex","{{BiometricDictamenKeys.MatchScore}}":{{score}},"{{BiometricDictamenKeys.LivenessPassed}}":{{liveness}},"{{BiometricDictamenKeys.Verdict}}":"{{verdict}}","model":"face-match-liveness-v1"}""";
    }
}
