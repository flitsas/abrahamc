using System.Text.Json;
using System.Text.RegularExpressions;

namespace Flit.Modules.ProceduresConfig.Domain;

/// <summary>Valida field_mapping jsonb del catálogo de endpoints (AC4 #9693).</summary>
public static partial class FieldMappingValidator
{
    private static readonly Regex FieldKeyPattern = FieldKeyRegex();
    private static readonly Regex PathSegmentPattern = PathSegmentRegex();

    public static bool TryValidate(JsonElement fieldMapping, out string? error)
    {
        error = null;

        if (fieldMapping.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return true;
        }

        if (fieldMapping.ValueKind != JsonValueKind.Object)
        {
            error = "field_mapping debe ser un objeto JSON.";
            return false;
        }

        foreach (var prop in fieldMapping.EnumerateObject())
        {
            if (string.IsNullOrWhiteSpace(prop.Name) || !FieldKeyPattern.IsMatch(prop.Name))
            {
                error = $"Clave de formulario inválida: '{prop.Name}'.";
                return false;
            }

            if (prop.Value.ValueKind != JsonValueKind.String)
            {
                error = $"field_mapping['{prop.Name}'] debe ser una ruta dot-path (string).";
                return false;
            }

            var path = prop.Value.GetString();
            if (string.IsNullOrWhiteSpace(path) || !IsValidDotPath(path))
            {
                error = $"Ruta inválida en field_mapping['{prop.Name}'].";
                return false;
            }
        }

        return true;
    }

    private static bool IsValidDotPath(string path)
    {
        foreach (var segment in path.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (!PathSegmentPattern.IsMatch(segment))
            {
                return false;
            }
        }

        return path.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Length > 0;
    }

    [GeneratedRegex(@"^[a-z][a-z0-9_]{0,63}$", RegexOptions.CultureInvariant)]
    private static partial Regex FieldKeyRegex();

    [GeneratedRegex(@"^[a-zA-Z_][a-zA-Z0-9_]*$", RegexOptions.CultureInvariant)]
    private static partial Regex PathSegmentRegex();
}
