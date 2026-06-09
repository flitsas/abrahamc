using Flit.Modules.IdentityVerification.Ports;

namespace Flit.Infrastructure.Adapters;

/// <summary>
/// Envío de invitaciones IDSecure vía <see cref="SmtpEmailService"/>.
/// </summary>
public sealed class SmtpInvitationEmailSender(SmtpEmailService smtpEmailService) : IInvitationEmailSender
{
    public Task SendHtmlAsync(
        string recipientEmail,
        string subject,
        string htmlBody,
        CancellationToken ct) =>
        smtpEmailService.SendHtmlAsync(recipientEmail, subject, htmlBody, ct);
}

public sealed class ConfigurationIdSecurePublicUrlProvider(Microsoft.Extensions.Configuration.IConfiguration config)
    : Flit.Modules.IdentityVerification.Ports.IIdSecurePublicUrlProvider
{
    public string GetPublicBaseUrl() =>
        config["IdSecure:PublicBaseUrl"] ?? "http://localhost:4001";
}
