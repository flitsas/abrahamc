using System.Text.Json;

namespace Flit.Modules.ProceduresConfig.Domain;

/// <summary>Aplica mapeo semántico respuesta API → campos de formulario (AC2 #9693).</summary>
public static class FieldMappingTransformer
{
    public static IReadOnlyDictionary<string, string?> Transform(
        JsonElement fieldMapping,
        JsonElement? responseBody)
    {
        if (fieldMapping.ValueKind != JsonValueKind.Object ||
            responseBody is null ||
            responseBody.Value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return new Dictionary<string, string?>();
        }

        return MarkerMapResolver.Resolve(fieldMapping, responseBody.Value);
    }
}
