using System.Text.Json;

namespace Flit.Modules.Companies.Domain;

/// <summary>
/// Evalúa <c>condition</c> JSONB contra contexto de radicación (#9448).
/// Campos soportados (AND): plates[], plate_prefix, procedure_types[], min_model_year, max_model_year.
/// </summary>
public static class VehicleOwnershipConditionMatcher
{
    public sealed record Context(
        string Plate,
        string? ProcedureType = null,
        int? ModelYear = null);

    public static bool Matches(string conditionJson, Context context)
    {
        if (string.IsNullOrWhiteSpace(conditionJson) || conditionJson.Trim() == "{}")
            return true;

        try
        {
            using var doc = JsonDocument.Parse(conditionJson);
            var root = doc.RootElement;
            var plate = context.Plate.Trim().ToUpperInvariant();

            if (root.TryGetProperty("plates", out var plates) && plates.ValueKind == JsonValueKind.Array)
            {
                var any = plates.EnumerateArray()
                    .Select(p => p.GetString()?.Trim().ToUpperInvariant())
                    .Where(p => !string.IsNullOrEmpty(p))
                    .Any(p => p == plate);
                if (!any)
                    return false;
            }

            if (root.TryGetProperty("plate_prefix", out var prefixEl))
            {
                var prefix = prefixEl.GetString()?.Trim().ToUpperInvariant();
                if (!string.IsNullOrEmpty(prefix) && !plate.StartsWith(prefix, StringComparison.Ordinal))
                    return false;
            }

            if (root.TryGetProperty("procedure_types", out var procTypes) && procTypes.ValueKind == JsonValueKind.Array)
            {
                var proc = context.ProcedureType?.Trim().ToLowerInvariant();
                if (string.IsNullOrEmpty(proc))
                    return false;

                var any = procTypes.EnumerateArray()
                    .Select(p => p.GetString()?.Trim().ToLowerInvariant())
                    .Any(p => p == proc);
                if (!any)
                    return false;
            }

            if (root.TryGetProperty("min_model_year", out var minYear) && minYear.TryGetInt32(out var min))
            {
                if (context.ModelYear is null || context.ModelYear < min)
                    return false;
            }

            if (root.TryGetProperty("max_model_year", out var maxYear) && maxYear.TryGetInt32(out var max))
            {
                if (context.ModelYear is null || context.ModelYear > max)
                    return false;
            }

            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
