using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;

namespace Flit.Infrastructure.Adapters;

/// <summary>
/// Cliente SMTP reutilizable (Office365, relay corporativo, MailHog).
/// </summary>
public sealed class SmtpEmailService(IOptions<SmtpOptions> options)
{
    public async Task SendHtmlAsync(
        string recipientEmail,
        string subject,
        string htmlBody,
        CancellationToken ct = default)
    {
        var opts = options.Value;
        if (string.IsNullOrWhiteSpace(opts.Host))
            throw new InvalidOperationException(
                "SMTP no configurado. Define SMTP_* en el archivo env de la raíz o la sección 'Smtp'.");

        if (string.IsNullOrWhiteSpace(opts.From))
            throw new InvalidOperationException("Smtp:From es requerido.");

        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(opts.FromName ?? opts.From, opts.From));
        message.To.Add(MailboxAddress.Parse(recipientEmail));
        message.Subject = subject;
        message.Body = new TextPart("html") { Text = htmlBody };

        using var client = new SmtpClient();
        var secureSocket = ResolveSecureSocketOptions(opts);

        await client.ConnectAsync(opts.Host, opts.Port, secureSocket, ct);

        if (!string.IsNullOrWhiteSpace(opts.User))
        {
            await client.AuthenticateAsync(opts.User, opts.Password ?? string.Empty, ct);
        }

        await client.SendAsync(message, ct);
        await client.DisconnectAsync(true, ct);
    }

    private static SecureSocketOptions ResolveSecureSocketOptions(SmtpOptions opts) =>
        opts.Port switch
        {
            465 => SecureSocketOptions.SslOnConnect,
            587 => SecureSocketOptions.StartTls,
            _ => opts.EnableSsl ? SecureSocketOptions.StartTls : SecureSocketOptions.None,
        };
}
