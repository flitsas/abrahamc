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

    [Fact]
    public void FindEnvFile_prefers_env_local_over_env()
    {
        var root = Path.Combine(Path.GetTempPath(), "flit-env-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        File.WriteAllText(Path.Combine(root, "package.json"), "{}");
        File.WriteAllText(Path.Combine(root, "env"), "CONNECTION_STRING_CORE=from-env");
        File.WriteAllText(Path.Combine(root, "env.local"), "CONNECTION_STRING_CORE=from-local");

        var previousCwd = Directory.GetCurrentDirectory();
        try
        {
            Directory.SetCurrentDirectory(root);
            var found = FlitEnvFileLoader.FindEnvFile();
            Assert.Equal(Path.Combine(root, "env.local"), found);
        }
        finally
        {
            Directory.SetCurrentDirectory(previousCwd);
            Directory.Delete(root, recursive: true);
        }
    }
}
