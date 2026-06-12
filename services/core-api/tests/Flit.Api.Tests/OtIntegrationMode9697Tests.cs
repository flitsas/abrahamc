using Flit.Modules.Companies.Domain;
using FluentAssertions;

namespace Flit.Api.Tests;

public sealed class OtIntegrationMode9697Tests
{
    [Theory]
    [InlineData("dashboard", "dashboard")]
    [InlineData("quipux", "qx")]
    [InlineData("QX", "qx")]
    public void NormalizeMode_MapsApiValues(string input, string expected)
    {
        OtQxIntegration.NormalizeMode(input).Should().Be(expected);
    }

    [Fact]
    public void ValidateMode_RejectsUnknownValues()
    {
        OtQxIntegration.ValidateMode("legacy").Should().NotBeNull();
    }

    [Fact]
    public void ChangeMode_UpdatesWithoutRecreatingEntity()
    {
        var actor = Guid.Parse("00000000-0000-7000-8000-000000000001");
        var now = DateTimeOffset.UtcNow;
        var integration = OtQxIntegration.Create(
            Guid.Parse("01930101-0001-7001-8001-000000000001"),
            "dashboard",
            actor,
            now);

        integration.ChangeMode("quipux", actor, now.AddMinutes(1));

        integration.Mode.Should().Be("qx");
        integration.RowVersion.Should().Be(2);
    }
}
