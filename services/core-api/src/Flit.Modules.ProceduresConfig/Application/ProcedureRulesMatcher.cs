using System.Text.Json;
using Flit.Modules.ProceduresConfig.Domain;
using Flit.Modules.ProceduresConfig.Ports;

namespace Flit.Modules.ProceduresConfig.Application;

internal static class ProcedureRulesMatcher
{
    public static async Task<IReadOnlyList<EvaluateProcedureRules.MatchedRuleDto>> MatchAsync(
        Guid tenantId,
        Guid procedureTypeId,
        IReadOnlyDictionary<string, string?> capturedFields,
        JsonDocument? configSnapshot,
        IProcedureRulesRepository rulesRepo,
        CancellationToken ct)
    {
        var rules = configSnapshot is not null
            ? ConfigSnapshotRules.Parse(configSnapshot)
            : await rulesRepo.ListActiveForEvaluationAsync(tenantId, procedureTypeId, ct);

        var matched = new List<EvaluateProcedureRules.MatchedRuleDto>();

        foreach (var rule in rules)
        {
            if (!RuleConditionEvaluator.Evaluate(rule.ConditionTree, capturedFields))
            {
                continue;
            }

            var actions = ParseActions(rule.Actions);
            matched.Add(new EvaluateProcedureRules.MatchedRuleDto(
                rule.Id,
                rule.Name,
                rule.Priority,
                actions));
        }

        return matched;
    }

    internal static List<EvaluateProcedureRules.RuleActionResult> ParseActions(JsonElement actionsNode)
    {
        if (actionsNode.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var list = new List<EvaluateProcedureRules.RuleActionResult>();
        foreach (var action in actionsNode.EnumerateArray())
        {
            if (action.ValueKind != JsonValueKind.Object ||
                !action.TryGetProperty("type", out var typeEl))
            {
                continue;
            }

            var type = typeEl.GetString();
            if (string.IsNullOrWhiteSpace(type))
            {
                continue;
            }

            var parameters = action.TryGetProperty("params", out var paramsEl)
                ? paramsEl.Clone()
                : ExtractNonTypeProperties(action);

            list.Add(new EvaluateProcedureRules.RuleActionResult(type, parameters));
        }

        return list;
    }

    private static JsonElement ExtractNonTypeProperties(JsonElement action)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            foreach (var prop in action.EnumerateObject())
            {
                if (prop.NameEquals("type"))
                {
                    continue;
                }

                prop.WriteTo(writer);
            }

            writer.WriteEndObject();
        }

        using var doc = JsonDocument.Parse(stream.ToArray());
        return doc.RootElement.Clone();
    }
}
