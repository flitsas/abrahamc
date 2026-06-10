using Flit.Infrastructure.Configuration;

namespace Flit.Api.Tests;

public sealed class FlitEnvFileLoaderTests
{
    [Theory]
    [InlineData("SMTP_PASSWORD='TramitesNoti2024*'", "SMTP_PASSWORD", "TramitesNoti2024*")]
    [InlineData("SMTP_HOST=smtp.office365.com", "SMTP_HOST", "smtp.office365.com")]
    [InlineData("  # comentario", null, null)]
    [InlineData("", null, null)]
    public void TryParseLine_parses_env_entries(string line, string? expectedKey, string? expectedValue)
    {
        var parsed = FlitEnvFileLoader.TryParseLine(line);

        if (expectedKey is null)
        {
            Assert.Null(parsed);
            return;
        }

        Assert.NotNull(parsed);
        Assert.Equal(expectedKey, parsed.Value.Key);
        Assert.Equal(expectedValue, parsed.Value.Value);
    }
}
