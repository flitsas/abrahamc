using System.Text.Json;

namespace Flit.Modules.ProceduresConfig.Domain;

public static class JsonRuleElements
{
    public static JsonElement Parse(string json)
    {
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.Clone();
    }
}
