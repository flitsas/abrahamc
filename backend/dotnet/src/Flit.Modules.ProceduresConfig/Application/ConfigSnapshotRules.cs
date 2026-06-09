using System.Text.Json;
using Flit.Modules.ProceduresConfig.Domain;

namespace Flit.Modules.ProceduresConfig.Application;

/// <summary>
/// Extrae reglas del <c>config_snapshot</c> inmutable (ADR-0010). AC2 #9438: radicados siguen con su set congelado.
/// </summary>
public static class ConfigSnapshotRules
{
    public static IReadOnlyList<ProcedureRuleRecord> Parse(JsonDocument? snapshot)
    {
        if (snapshot is null)
        {
            return [];
        }

        var root = snapshot.RootElement;
        if (!root.TryGetProperty("rules", out var rulesEl) || rulesEl.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var list = new List<ProcedureRuleRecord>();
        foreach (var item in rulesEl.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            if (!item.TryGetProperty("id", out var idEl) || !Guid.TryParse(idEl.GetString(), out var id))
            {
                continue;
            }

            var name = item.TryGetProperty("name", out var nameEl) ? nameEl.GetString() ?? id.ToString() : id.ToString();
            var priority = item.TryGetProperty("priority", out var priEl) && priEl.TryGetInt32(out var p) ? p : 100;

            if (!item.TryGetProperty("condition_tree", out var cond))
            {
                continue;
            }

            var actions = item.TryGetProperty("actions", out var act)
                ? act
                : JsonRuleElements.Parse("[]");

            list.Add(new ProcedureRuleRecord(id, name, priority, cond.Clone(), actions.Clone()));
        }

        return list
            .OrderBy(r => r.Priority)
            .ThenBy(r => r.Name, StringComparer.Ordinal)
            .ToList();
    }
}
