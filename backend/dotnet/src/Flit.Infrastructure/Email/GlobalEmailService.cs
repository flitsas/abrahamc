using Flit.Infrastructure.Adapters;
using Flit.Modules.Notifications.Domain;
using Flit.Modules.Notifications.Ports;
using Microsoft.Extensions.Options;

namespace Flit.Infrastructure.Email;

/// <summary>
/// Envío SMTP global con plantillas PostgreSQL (#9549 HU #9682).
/// </summary>
public sealed class GlobalEmailService(
  IOptions<SmtpOptions> options,
  SmtpEmailService smtp,
  IEmailTemplateRepository templates) : IGlobalEmailService
{
  public bool IsConfigured => options.Value.IsConfigured;

  public async Task SendTemplatedAsync(
    string recipientEmail,
    string templateKey,
    IReadOnlyDictionary<string, string> variables,
    string locale = "es-CO",
    CancellationToken ct = default)
  {
    ArgumentException.ThrowIfNullOrWhiteSpace(recipientEmail);
    ArgumentException.ThrowIfNullOrWhiteSpace(templateKey);

    var template = await templates.GetActiveAsync(templateKey, locale, ct)
      ?? throw new InvalidOperationException(
        $"Plantilla SMTP '{templateKey}' ({locale}) no encontrada o inactiva.");

    var subject = EmailTemplateRenderer.Render(template.Subject, variables);
    var body = EmailTemplateRenderer.Render(template.HtmlBody, variables);
    body = EmailTemplateRenderer.EnrichWithActivationLink(body, variables);
    body = EmailTemplateRenderer.EnrichWithResetLink(body, variables);

    await SendHtmlAsync(recipientEmail, subject, body, ct);
  }

  public async Task SendHtmlAsync(
    string recipientEmail,
    string subject,
    string htmlBody,
    CancellationToken ct = default)
  {
    ArgumentException.ThrowIfNullOrWhiteSpace(recipientEmail);
    ArgumentException.ThrowIfNullOrWhiteSpace(subject);
    ArgumentException.ThrowIfNullOrWhiteSpace(htmlBody);

    if (!IsConfigured)
      throw new InvalidOperationException("SMTP no configurado. Define la sección 'Smtp' en appsettings.");

    await smtp.SendHtmlAsync(recipientEmail, subject, htmlBody, ct);
  }
}
