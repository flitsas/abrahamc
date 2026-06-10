using System.Net;

namespace Flit.Modules.Notifications.Domain;

public static class EmailTemplateRenderer
{
  public static string Render(string template, IReadOnlyDictionary<string, string> variables)
  {
    var result = template;
    foreach (var (key, value) in variables)
    {
      var encoded = WebUtility.HtmlEncode(value);
      result = result.Replace($"{{{{{key}}}}}", encoded, StringComparison.Ordinal);
    }

    return result;
  }

  /// <summary>
  /// Enriquece el cuerpo con CTA estándar si la plantilla no incluye el enlace de activación.
  /// </summary>
  public static string EnrichWithActivationLink(
    string htmlBody,
    IReadOnlyDictionary<string, string> variables) =>
    EnrichWithActionLink(htmlBody, variables, "activation_url", "Activar mi cuenta");

  /// <summary>
  /// Enriquece el cuerpo con CTA estándar si la plantilla no incluye el enlace de reset.
  /// </summary>
  public static string EnrichWithResetLink(
    string htmlBody,
    IReadOnlyDictionary<string, string> variables) =>
    EnrichWithActionLink(htmlBody, variables, "reset_url", "Restablecer contraseña");

  private static string EnrichWithActionLink(
    string htmlBody,
    IReadOnlyDictionary<string, string> variables,
    string variableKey,
    string buttonLabel)
  {
    if (!variables.TryGetValue(variableKey, out var url)
        || string.IsNullOrWhiteSpace(url)
        || htmlBody.Contains(url, StringComparison.Ordinal)
        || htmlBody.Contains($"{{{{{variableKey}}}}}", StringComparison.Ordinal))
    {
      return htmlBody;
    }

    var safeUrl = WebUtility.HtmlEncode(url);
    return htmlBody + $"""
      <p style="margin-top:16px;">
        <a href="{safeUrl}"
           style="display:inline-block;padding:12px 20px;background:#2563eb;color:#fff;
                  text-decoration:none;border-radius:6px;">{buttonLabel}</a>
      </p>
      <p style="font-size:12px;color:#666;word-break:break-all;">{safeUrl}</p>
      """;
  }
}
