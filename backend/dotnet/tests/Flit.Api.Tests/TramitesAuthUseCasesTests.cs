using Flit.Modules.Identity.Application;
using FluentAssertions;

namespace Flit.Api.Tests;

public sealed class TramitesAuthUseCasesTests
{
    [Fact]
    public void HashRefreshToken_IsDeterministic()
    {
        var hash1 = TramitesAuthUseCases.HashRefreshToken("opaque-token-abc");
        var hash2 = TramitesAuthUseCases.HashRefreshToken("opaque-token-abc");

        hash1.Should().Be(hash2);
        hash1.Should().HaveLength(64);
    }

    [Fact]
    public void GenerateOpaqueRefreshToken_ProducesUniqueValues()
    {
        var a = TramitesAuthUseCases.GenerateOpaqueRefreshToken();
        var b = TramitesAuthUseCases.GenerateOpaqueRefreshToken();

        a.Should().NotBe(b);
        a.Should().NotContain("=");
    }
}
