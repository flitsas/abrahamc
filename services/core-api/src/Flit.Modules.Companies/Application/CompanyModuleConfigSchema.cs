using System.Text;
using System.Text.Json;
using Flit.Modules.Companies.Domain;

namespace Flit.Modules.Companies.Application;

/// <summary>Validación y enmascaramiento de config JSON por module_key (#9688).</summary>
public static class CompanyModuleConfigSchema
{
    public static string? Validate(string moduleKey, string configJson)
    {
        var key = CompanyModuleKey.Normalize(moduleKey);
        try
        {
            using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(configJson) ? "{}" : configJson);
            return key switch
            {
                CompanyModuleKey.Recaudo => ValidateRecaudo(doc.RootElement),
                _ => null,
            };
        }
        catch (JsonException)
        {
            return "config debe ser JSON válido.";
        }
    }

    public static string MaskForResponse(string moduleKey, string configJson)
    {
        var key = CompanyModuleKey.Normalize(moduleKey);
        if (key != CompanyModuleKey.Company)
        {
            return configJson;
        }

        try
        {
            using var doc = JsonDocument.Parse(configJson);
            if (!doc.RootElement.TryGetProperty("smtp_settings", out var smtp)
                || smtp.ValueKind != JsonValueKind.Object)
            {
                return configJson;
            }

            using var stream = new MemoryStream();
            using (var writer = new Utf8JsonWriter(stream))
            {
                writer.WriteStartObject();
                foreach (var prop in doc.RootElement.EnumerateObject())
                {
                    if (prop.NameEquals("smtp_settings"))
                    {
                        writer.WritePropertyName("smtp_settings");
                        writer.WriteStartObject();
                        foreach (var smtpProp in smtp.EnumerateObject())
                        {
                            if (smtpProp.NameEquals("password") || smtpProp.NameEquals("smtp_password"))
                            {
                                writer.WriteString(smtpProp.Name, "***");
                            }
                            else
                            {
                                smtpProp.WriteTo(writer);
                            }
                        }

                        writer.WriteEndObject();
                    }
                    else
                    {
                        prop.WriteTo(writer);
                    }
                }

                writer.WriteEndObject();
            }

            return Encoding.UTF8.GetString(stream.ToArray());
        }
        catch (JsonException)
        {
            return configJson;
        }
    }

    private static string? ValidateRecaudo(JsonElement root)
    {
        if (root.TryGetProperty("payment_methods", out var methods)
            && methods.ValueKind == JsonValueKind.Array
            && methods.GetArrayLength() > 0)
        {
            return null;
        }

        if (root.TryGetProperty("default_payment_method", out var single)
            && single.ValueKind == JsonValueKind.String
            && !string.IsNullOrWhiteSpace(single.GetString()))
        {
            return null;
        }

        return "recaudo requiere payment_methods (array no vacío) o default_payment_method.";
    }
}
