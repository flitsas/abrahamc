using System.Text.Json;

namespace Flit.Modules.ProceduresConfig.Domain;

/// <summary>Resuelve marcadores del <c>marker_map</c> contra un contexto JSON plano (HU #9442).</summary>
public static class MarkerMapResolver
{
    public static IReadOnlyDictionary<string, string?> Resolve(
        JsonElement markerMap,
        JsonElement dataContext)
    {
        if (markerMap.ValueKind != JsonValueKind.Object)
            return new Dictionary<string, string?>();

        var result = new Dictionary<string, string?>(StringComparer.Ordinal);
        foreach (var prop in markerMap.EnumerateObject())
        {
            var path = prop.Value.GetString() ?? prop.Value.ToString();
            result[prop.Name] = TryGetByPath(dataContext, path);
        }

        return result;
    }

    public static string? TryGetByPath(JsonElement root, string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return null;

        var current = root;
        foreach (var segment in path.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (current.ValueKind != JsonValueKind.Object)
                return null;
            if (!current.TryGetProperty(segment, out var next))
                return null;
            current = next;
        }

        return current.ValueKind switch
        {
            JsonValueKind.String => current.GetString(),
            JsonValueKind.Number => current.GetRawText(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            JsonValueKind.Null => null,
            _ => current.GetRawText(),
        };
    }
}
