using Flit.Infrastructure.Options;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;

namespace Flit.Infrastructure.Email;

/// <summary>Envío SMTP vía MailKit.</summary>
public sealed class SmtpEmailSender(IOptions<SmtpEmailOptions> options)
{
    public async Task SendHtmlAsync(
        string toEmail,
        string subject,
        string htmlBody,
        CancellationToken ct = default)
    {
        var opts = options.Value;
        if (!opts.IsConfigured)
            throw new InvalidOperationException("SMTP no está configurado.");

        var senderEmail = opts.DefaultSenderEmail!;
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(opts.DefaultSenderName, senderEmail));
        message.To.Add(MailboxAddress.Parse(toEmail));
        message.Subject = subject;
        message.Body = new TextPart("html") { Text = htmlBody };

        using var client = new SmtpClient();
        var socketOptions = opts.UseStartTls
            ? SecureSocketOptions.StartTls
            : SecureSocketOptions.None;

        await client.ConnectAsync(opts.Host!, opts.Port, socketOptions, ct);

        if (!opts.DisableAuthentication)
        {
            await client.AuthenticateAsync(
                opts.DefaultSenderEmail!, opts.DefaultSenderPassword!, ct);
        }

        await client.SendAsync(message, ct);
        await client.DisconnectAsync(quit: true, ct);
    }
}
