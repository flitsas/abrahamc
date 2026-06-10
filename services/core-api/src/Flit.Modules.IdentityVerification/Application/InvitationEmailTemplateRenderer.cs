namespace Flit.Modules.IdentityVerification.Application;

/// <summary>
/// Reemplaza placeholders {{validationUrl}} y {{expiresAt}} en plantillas HTML.
/// </summary>
public static class InvitationEmailTemplateRenderer
{
    public static (string Subject, string HtmlBody) Render(
        string subjectTemplate,
        string htmlBodyTemplate,
        string validationUrl,
        DateTimeOffset expiresAt)
    {
        var expiresFormatted = expiresAt.ToString("yyyy-MM-dd HH:mm 'UTC'", System.Globalization.CultureInfo.InvariantCulture);
        var subject = subjectTemplate
            .Replace("{{validationUrl}}", validationUrl, StringComparison.Ordinal)
            .Replace("{{expiresAt}}", expiresFormatted, StringComparison.Ordinal);
        var html = htmlBodyTemplate
            .Replace("{{validationUrl}}", validationUrl, StringComparison.Ordinal)
            .Replace("{{expiresAt}}", expiresFormatted, StringComparison.Ordinal);
        return (subject, html);
    }

    public static string BuildValidationUrl(string publicBaseUrl, string plainToken)
    {
        var baseUrl = publicBaseUrl.TrimEnd('/');
        return $"{baseUrl}/public/idsecure/session?token={Uri.EscapeDataString(plainToken)}";
    }
}
