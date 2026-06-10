using Flit.Infrastructure.Persistence;
using Flit.Modules.Notifications.Ports;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Flit.Infrastructure.Repositories;

public sealed class NpgsqlEmailTemplateRepository(FlitDbContext db) : IEmailTemplateRepository
{
  public async Task<EmailTemplateRow?> GetActiveAsync(
    string templateKey,
    string locale,
    CancellationToken ct = default)
  {
    var conn = (NpgsqlConnection)db.Database.GetDbConnection();
    if (conn.State != System.Data.ConnectionState.Open)
      await conn.OpenAsync(ct);

    await using var cmd = new NpgsqlCommand(
      """
      SELECT template_key, locale, subject, html_body
      FROM notifications.email_templates
      WHERE template_key = @templateKey
        AND locale = @locale
        AND is_active = true
      LIMIT 1
      """,
      conn);
    cmd.Parameters.AddWithValue("templateKey", templateKey);
    cmd.Parameters.AddWithValue("locale", locale);

    await using var reader = await cmd.ExecuteReaderAsync(ct);
    if (!await reader.ReadAsync(ct))
      return null;

    return new EmailTemplateRow(
      reader.GetString(0),
      reader.GetString(1),
      reader.GetString(2),
      reader.GetString(3));
  }
}
