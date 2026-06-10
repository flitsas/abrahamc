using Flit.Modules.Notifications.Domain;
using FluentAssertions;

namespace Flit.Api.Tests;

public sealed class EmailTemplateRendererTests
{
    [Fact]
    public void EnrichWithResetLink_AppendsButton_WhenTemplateHasNoLink()
    {
        const string body = "<p>Solicitaste restablecer tu contraseña.</p>";
        const string resetUrl = "http://localhost:5173/reset-password?token=abc123";

        var result = EmailTemplateRenderer.EnrichWithResetLink(
            body,
            new Dictionary<string, string> { ["reset_url"] = resetUrl });

        result.Should().Contain("Restablecer contraseña");
        result.Should().Contain(resetUrl);
    }

    [Fact]
    public void Render_ReplacesResetUrlPlaceholder()
    {
        const string template = "<a href=\"{{reset_url}}\">Restablecer</a>";
        const string resetUrl = "http://localhost:5173/reset-password?token=abc123";

        var result = EmailTemplateRenderer.Render(
            template,
            new Dictionary<string, string> { ["reset_url"] = resetUrl });

        result.Should().Be($"<a href=\"{resetUrl}\">Restablecer</a>");
    }
}
