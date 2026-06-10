using Flit.Modules.IdentityVerification.Domain;
using Flit.Modules.IdentityVerification.Ports;

namespace Flit.Modules.IdentityVerification.Adapters;

/// <summary>Proveedor DEV (TRA-03). Simula SMS, email, OTP y liveness sin llamadas externas.</summary>
public sealed class MockIdentityVerificationProvider : IIdentityVerificationProvider
{
    public static int LivenessInvocationCount { get; private set; }

    public static void ResetCounters() => LivenessInvocationCount = 0;

    public string ProviderName => "mock";

    public Task<ProviderVerificationResult> RunChannelsAsync(
        IReadOnlyList<string> channels,
        CancellationToken ct = default)
    {
        var completed = new List<string>();
        var livenessRan = false;

        foreach (var channel in channels)
        {
            ct.ThrowIfCancellationRequested();
            if (string.Equals(channel, VerificationChannels.Liveness, StringComparison.OrdinalIgnoreCase))
            {
                LivenessInvocationCount++;
                livenessRan = true;
            }

            completed.Add(channel);
        }

        return Task.FromResult(new ProviderVerificationResult(
            VerificationStatuses.Passed,
            "approved",
            99.5m,
            completed,
            livenessRan));
    }
}
