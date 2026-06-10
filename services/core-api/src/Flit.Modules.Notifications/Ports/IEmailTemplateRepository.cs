namespace Flit.Modules.Notifications.Ports;

public sealed record EmailTemplateRow(
  string TemplateKey,
  string Locale,
  string Subject,
  string HtmlBody);

public interface IEmailTemplateRepository
{
  Task<EmailTemplateRow?> GetActiveAsync(
    string templateKey,
    string locale,
    CancellationToken ct = default);
}
