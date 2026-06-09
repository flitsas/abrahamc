using System.Text.Json;

namespace Flit.Modules.Integrations.Domain;

/// <summary>Envoltura de payload con metadatos de idempotencia (sin columna extra en ddl/70).</summary>
public static class ExternalQueryPayloadMetadata
{
    public const string MetaPropertyName = "_flit";

    public static JsonElement Wrap(JsonElement businessPayload, string idempotencyKey)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            writer.WritePropertyName(MetaPropertyName);
            writer.WriteStartObject();
            writer.WriteString("idempotency_key", idempotencyKey);
            writer.WriteEndObject();
            writer.WritePropertyName("payload");
            businessPayload.WriteTo(writer);
            writer.WriteEndObject();
        }

        using var doc = JsonDocument.Parse(stream.ToArray());
        return doc.RootElement.Clone();
    }

    public static string? TryGetIdempotencyKey(JsonElement storedRequest)
    {
        if (storedRequest.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        if (!storedRequest.TryGetProperty(MetaPropertyName, out var meta) ||
            meta.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        return meta.TryGetProperty("idempotency_key", out var key) && key.ValueKind == JsonValueKind.String
            ? key.GetString()
            : null;
    }

    public static JsonElement UnwrapBusinessPayload(JsonElement storedRequest)
    {
        if (storedRequest.TryGetProperty("payload", out var payload))
        {
            return payload.Clone();
        }

        return storedRequest.Clone();
    }
}
