using Flit.Modules.Notifications.Ports;

namespace Flit.Infrastructure.Email;

/// <summary>Sin envío SMTP cuando la sección Smtp no está configurada (DEV).</summary>
public sealed class NoOpGlobalEmailService : IGlobalEmailService
{
  public bool IsConfigured => false;

  public Task SendTemplatedAsync(
    string recipientEmail,
    string templateKey,
    IReadOnlyDictionary<string, string> variables,
    string locale = "es-CO",
    CancellationToken ct = default) =>
    Task.CompletedTask;

  public Task SendHtmlAsync(
    string recipientEmail,
    string subject,
    string htmlBody,
    CancellationToken ct = default) =>
    Task.CompletedTask;
}
