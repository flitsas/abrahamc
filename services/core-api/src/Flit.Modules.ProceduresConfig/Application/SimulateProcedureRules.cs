using System.Text.Json;
using Flit.Modules.ProceduresConfig.Domain;
using Flit.Modules.ProceduresConfig.Ports;

namespace Flit.Modules.ProceduresConfig.Application;

/// <summary>HU #9694 AC2 — dry-run de reglas sin escrituras ni invocaciones externas.</summary>
public static class SimulateProcedureRules
{
    public sealed record Query(
        Guid TenantId,
        Guid ProcedureTypeId,
        IReadOnlyDictionary<string, string?> CapturedFields,
        JsonDocument? ConfigSnapshot = null);

    public sealed record ConflictDto(
        string ConflictType,
        string Target,
        IReadOnlyList<string> ActionTypes,
        IReadOnlyList<Guid> RuleIds);

    public sealed record Response(
        IReadOnlyList<EvaluateProcedureRules.MatchedRuleDto> MatchedRules,
        IReadOnlyList<EvaluateProcedureRules.RuleActionResult> Actions,
        IReadOnlyList<ConflictDto> Conflicts);

    public static async Task<Response> HandleAsync(
        Query query,
        IProcedureRulesRepository rulesRepo,
        CancellationToken ct = default)
    {
        var matched = await ProcedureRulesMatcher.MatchAsync(
            query.TenantId,
            query.ProcedureTypeId,
            query.CapturedFields,
            query.ConfigSnapshot,
            rulesRepo,
            ct);

        var flatActions = matched
            .SelectMany(m => m.Actions.Select(a => (m.RuleId, a)))
            .ToList();

        var conflicts = RuleActionConflictDetector
            .Detect(flatActions)
            .Select(c => new ConflictDto(
                c.ConflictType,
                c.Target,
                c.ActionTypes,
                c.RuleIds))
            .ToList();

        var allActions = matched.SelectMany(m => m.Actions).ToList();

        return new Response(matched, allActions, conflicts);
    }
}
