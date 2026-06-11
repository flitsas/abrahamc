using Flit.Modules.Identity.Application;
using Xunit;

namespace Flit.Api.Tests;

public sealed class TramitesOnboardingTokenTests
{
    private static readonly byte[] SigningKey = [1, 2, 3, 4];

    [Fact]
    public void VerifySignature_FailsWhenExpiresAtDriftsAfterPostgresRoundTrip()
    {
        var invitationId = Guid.Parse("019eb785-db0e-7137-b74f-1b33d3894244");
        var token = "SYn-oaBvYi1ScMrj8CtBmqDvnedFOREhow3HNkjAkEw";
        var inMemory = new DateTimeOffset(2026, 6, 12, 15, 30, 45, 123, TimeSpan.Zero).AddTicks(4567);
        var postgres = TramitesOnboardingToken.NormalizeExpiresAtForStorage(inMemory);

        var signature = TramitesOnboardingToken.ComputeSignature(
            invitationId, token, inMemory, SigningKey);

        Assert.NotEqual(inMemory, postgres);
        Assert.False(TramitesOnboardingToken.VerifySignature(
            invitationId, token, postgres, signature, SigningKey));
        Assert.True(TramitesOnboardingToken.VerifyStoredSignature(signature, signature));
    }

    [Fact]
    public void NormalizeExpiresAtForStorage_MatchesPostgresMicrosecondPrecision()
    {
        var value = new DateTimeOffset(2026, 6, 12, 15, 30, 45, 123, TimeSpan.Zero).AddTicks(4567);
        var normalized = TramitesOnboardingToken.NormalizeExpiresAtForStorage(value);

        var signatureBefore = TramitesOnboardingToken.ComputeSignature(
            Guid.NewGuid(), "token", value, SigningKey);
        var signatureAfter = TramitesOnboardingToken.ComputeSignature(
            Guid.NewGuid(), "token", normalized, SigningKey);

        Assert.NotEqual(signatureBefore, signatureAfter);
        Assert.Equal(0, normalized.UtcTicks % 10);
    }
}
