using System.Text.Json;
using Flit.Modules.ProceduresConfig.Application;

namespace Flit.Modules.ProceduresConfig.Domain;

/// <summary>Detecta conflictos entre acciones de reglas coincidentes (HU #9694).</summary>
public static class RuleActionConflictDetector
{
    public const string BlockVsInjectSection = "block_vs_inject_section";
    public const string GlobalBlockVsInjectSection = "global_block_vs_inject_section";

    public sealed record Conflict(
        string ConflictType,
        string Target,
        IReadOnlyList<string> ActionTypes,
        IReadOnlyList<Guid> RuleIds);

    public static IReadOnlyList<Conflict> Detect(
        IReadOnlyList<(Guid RuleId, EvaluateProcedureRules.RuleActionResult Action)> actions)
    {
        var byTarget = new Dictionary<string, TargetBucket>(StringComparer.Ordinal);
        var globalBlockRuleIds = new List<Guid>();

        foreach (var (ruleId, action) in actions)
        {
            if (string.Equals(action.Type, "block", StringComparison.Ordinal))
            {
                var target = ResolveTarget(action);
                if (string.IsNullOrWhiteSpace(target))
                {
                    globalBlockRuleIds.Add(ruleId);
                    continue;
                }

                GetBucket(byTarget, target).Blocks.Add(ruleId);
                continue;
            }

            if (string.Equals(action.Type, "inject_section", StringComparison.Ordinal))
            {
                var target = ResolveTarget(action);
                if (string.IsNullOrWhiteSpace(target))
                {
                    continue;
                }

                GetBucket(byTarget, target).InjectSections.Add(ruleId);
            }
        }

        var conflicts = new List<Conflict>();

        foreach (var (target, bucket) in byTarget)
        {
            if (bucket.Blocks.Count > 0 && bucket.InjectSections.Count > 0)
            {
                conflicts.Add(new Conflict(
                    BlockVsInjectSection,
                    target,
                    ["block", "inject_section"],
                    bucket.Blocks.Concat(bucket.InjectSections).Distinct().ToList()));
            }
        }

        if (globalBlockRuleIds.Count > 0)
        {
            var injectRuleIds = byTarget.Values
                .SelectMany(b => b.InjectSections)
                .Distinct()
                .ToList();

            if (injectRuleIds.Count > 0)
            {
                conflicts.Add(new Conflict(
                    GlobalBlockVsInjectSection,
                    "__global__",
                    ["block", "inject_section"],
                    globalBlockRuleIds.Concat(injectRuleIds).Distinct().ToList()));
            }
        }

        return conflicts;
    }

    private static string? ResolveTarget(EvaluateProcedureRules.RuleActionResult action)
    {
        if (action.Params.ValueKind == JsonValueKind.Object)
        {
            if (action.Params.TryGetProperty("target", out var targetEl))
            {
                return targetEl.GetString();
            }

            if (action.Params.TryGetProperty("section_code", out var sectionEl))
            {
                return sectionEl.GetString();
            }
        }

        return null;
    }

    private static TargetBucket GetBucket(Dictionary<string, TargetBucket> map, string target)
    {
        if (!map.TryGetValue(target, out var bucket))
        {
            bucket = new TargetBucket();
            map[target] = bucket;
        }

        return bucket;
    }

    private sealed class TargetBucket
    {
        public List<Guid> Blocks { get; } = [];
        public List<Guid> InjectSections { get; } = [];
    }
}
