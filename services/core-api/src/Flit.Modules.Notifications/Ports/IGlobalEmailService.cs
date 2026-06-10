namespace Flit.Modules.Notifications.Ports;

/// <summary>
/// Servicio SMTP global transversal (#9549 / HU #9682).
/// Carga plantillas desde <c>notifications.email_templates</c> y envía correo HTML.
/// </summary>
public interface IGlobalEmailService
{
  bool IsConfigured { get; }

  Task SendTemplatedAsync(
    string recipientEmail,
    string templateKey,
    IReadOnlyDictionary<string, string> variables,
    string locale = "es-CO",
    CancellationToken ct = default);

  Task SendHtmlAsync(
    string recipientEmail,
    string subject,
    string htmlBody,
    CancellationToken ct = default);
}
