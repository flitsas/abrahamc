using Flit.Modules.Identity.Application;
using FluentAssertions;

namespace Flit.Api.Tests;

public sealed class TramitesPasswordResetUseCasesTests
{
    [Fact]
    public void HashToken_IsDeterministicSha256Hex()
    {
        var hash1 = TramitesPasswordResetUseCases.HashToken("reset-token-abc");
        var hash2 = TramitesPasswordResetUseCases.HashToken("reset-token-abc");

        hash1.Should().Be(hash2);
        hash1.Should().HaveLength(64);
    }
}
