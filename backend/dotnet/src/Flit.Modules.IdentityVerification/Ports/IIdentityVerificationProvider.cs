namespace Flit.Modules.IdentityVerification.Ports;

public interface IIdentityVerificationProvider
{
    string ProviderName { get; }

    /// <summary>Ejecuta los canales indicados (sms, email, otp, liveness). Incrementa contador de liveness en mock.</summary>
    Task<ProviderVerificationResult> RunChannelsAsync(
        IReadOnlyList<string> channels,
        CancellationToken ct = default);
}

public sealed record ProviderVerificationResult(
    string Status,
    string Verdict,
    decimal Score,
    IReadOnlyList<string> ChannelsCompleted,
    bool LivenessPerformed);
