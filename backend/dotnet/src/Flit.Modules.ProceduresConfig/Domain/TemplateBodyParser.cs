using System.Text.Json;

namespace Flit.Modules.ProceduresConfig.Domain;

public sealed record TemplateBodyLayout(string Title, string? Subtitle);

public static class TemplateBodyParser
{
    public static TemplateBodyLayout Parse(string? bodyInline, string templateName)
    {
        if (string.IsNullOrWhiteSpace(bodyInline))
            return new TemplateBodyLayout(templateName, null);

        try
        {
            using var doc = JsonDocument.Parse(bodyInline);
            var root = doc.RootElement;
            var title = root.TryGetProperty("title", out var t) && t.ValueKind == JsonValueKind.String
                ? t.GetString()!
                : templateName;
            string? subtitle = root.TryGetProperty("subtitle", out var s) && s.ValueKind == JsonValueKind.String
                ? s.GetString()
                : null;
            return new TemplateBodyLayout(title, subtitle);
        }
        catch (JsonException)
        {
            return new TemplateBodyLayout(bodyInline.Trim(), null);
        }
    }
}
