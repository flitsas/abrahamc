using System.Text.RegularExpressions;
using Flit.Modules.IdentityVerification.Ports;

namespace Flit.Infrastructure.Adapters;

/// <summary>
/// DEV: no envía SMTP; escribe el enlace de verificación en consola.
/// </summary>
public sealed partial class DevLoggingInvitationEmailSender : IInvitationEmailSender
{
    public Task SendHtmlAsync(
        string recipientEmail,
        string subject,
        string htmlBody,
        CancellationToken ct)
    {
        var link = ValidationLinkRegex().Match(htmlBody);
        var url = link.Success ? link.Groups[1].Value : "(link no encontrado en HTML)";
        Console.WriteLine(
            $"[IDSecure DEV] Email → To: {recipientEmail} | Subject: {subject} | Link: {url}");

        return Task.CompletedTask;
    }

    [GeneratedRegex("href=\"([^\"]+)\"", RegexOptions.IgnoreCase)]
    private static partial Regex ValidationLinkRegex();
}
