using System.Net;
using Flit.Infrastructure.Email;
using Flit.Modules.Identity.Application;

namespace Flit.Infrastructure.Adapters;

/// <summary>Envía invitación de onboarding por SMTP (HU #9418).</summary>
public sealed class SmtpOnboardingEmailNotifier(SmtpEmailSender sender) : IOnboardingEmailNotifier
{
    public async Task SendInvitationAsync(
        string email,
        string activationUrl,
        DateTimeOffset expiresAt,
        CancellationToken ct = default)
    {
        var subject = "Activa tu cuenta en FLIT Trámites";
        var expiresLocal = expiresAt.ToString("f");
        var html = $"""
            <!DOCTYPE html>
            <html lang="es">
            <body style="font-family:Segoe UI,Arial,sans-serif;color:#1a1a1a;">
              <h2>Invitación a FLIT Trámites</h2>
              <p>Has sido invitado a unirte a la plataforma. El enlace es válido por <strong>24 horas</strong>
                 (hasta {WebUtility.HtmlEncode(expiresLocal)} UTC).</p>
              <p><a href="{WebUtility.HtmlEncode(activationUrl)}"
                    style="display:inline-block;padding:12px 20px;background:#2563eb;color:#fff;
                    text-decoration:none;border-radius:6px;">Activar mi cuenta</a></p>
              <p style="font-size:12px;color:#666;">Si el botón no funciona, copia este enlace en el navegador:<br/>
              <span style="word-break:break-all;">{WebUtility.HtmlEncode(activationUrl)}</span></p>
              <p style="font-size:12px;color:#999;">Este mensaje es automático; no respondas a este correo.</p>
            </body>
            </html>
            """;

        await sender.SendHtmlAsync(email, subject, html, ct);
    }
}
