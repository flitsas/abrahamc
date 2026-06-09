using System.Text.Json;

namespace Flit.Modules.Integrations.Domain;

/// <summary>CF-I3 — evita PII innecesaria en request/response de bitácora.</summary>
public static class ExternalQueryLogSanitizer
{
    private static readonly HashSet<string> RedactedKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "documento",
        "document_number",
        "documentNumber",
        "numero_documento",
        "numeroDocumento",
        "nombre",
        "name",
        "email",
        "telefono",
        "phone",
        "vin",
        "placa",
    };

    public static JsonElement Sanitize(JsonElement source)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            WriteRedacted(source, writer);
        }

        using var doc = JsonDocument.Parse(stream.ToArray());
        return doc.RootElement.Clone();
    }

    private static void WriteRedacted(JsonElement element, Utf8JsonWriter writer)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                writer.WriteStartObject();
                foreach (var prop in element.EnumerateObject())
                {
                    writer.WritePropertyName(prop.Name);
                    if (RedactedKeys.Contains(prop.Name))
                    {
                        writer.WriteStringValue("[REDACTED]");
                    }
                    else
                    {
                        WriteRedacted(prop.Value, writer);
                    }
                }

                writer.WriteEndObject();
                break;
            case JsonValueKind.Array:
                writer.WriteStartArray();
                foreach (var item in element.EnumerateArray())
                {
                    WriteRedacted(item, writer);
                }

                writer.WriteEndArray();
                break;
            default:
                element.WriteTo(writer);
                break;
        }
    }
}
