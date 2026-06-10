using System.Text.Json;
using Flit.Modules.ProceduresConfig.Domain;
using Flit.Modules.ProceduresConfig.Ports;

namespace Flit.Modules.ProceduresConfig.Application;

/// <summary>
/// RGL-02 (#9438): evalúa reglas por prioridad; hot-swap (solo activas en BD); snapshot para radicados (AC2).
/// </summary>
public static class EvaluateProcedureRules
{
    public sealed record Query(
        Guid TenantId,
        Guid ProcedureTypeId,
        IReadOnlyDictionary<string, string?> CapturedFields,
        JsonDocument? ConfigSnapshot = null,
        Guid? ProcedureInstanceId = null);

    public sealed record RuleActionResult(string Type, JsonElement Params);

    public sealed record MatchedRuleDto(
        Guid RuleId,
        string RuleName,
        int Priority,
        IReadOnlyList<RuleActionResult> Actions);

    public sealed record Response(
        IReadOnlyList<MatchedRuleDto> MatchedRules,
        IReadOnlyList<RuleActionResult> Actions,
        IReadOnlyList<RuleEndpointInvocationResult> EndpointInvocations);

    public static async Task<Response> HandleAsync(
        Query query,
        IProcedureRulesRepository rulesRepo,
        IRuleEndpointInvoker endpointInvoker,
        CancellationToken ct = default)
    {
        var rules = query.ConfigSnapshot is not null
            ? ConfigSnapshotRules.Parse(query.ConfigSnapshot)
            : await rulesRepo.ListActiveForEvaluationAsync(
                query.TenantId,
                query.ProcedureTypeId,
                ct);

        var matched = new List<MatchedRuleDto>();
        var allActions = new List<RuleActionResult>();
        var endpointInvocations = new List<RuleEndpointInvocationResult>();

        foreach (var rule in rules)
        {
            if (!RuleConditionEvaluator.Evaluate(rule.ConditionTree, query.CapturedFields))
            {
                continue;
            }

            var actions = ParseActions(rule.Actions);
            matched.Add(new MatchedRuleDto(rule.Id, rule.Name, rule.Priority, actions));
            allActions.AddRange(actions);

            await InvokeEndpointCallsAsync(
                query,
                rule,
                actions,
                endpointInvoker,
                endpointInvocations,
                ct);
        }

        return new Response(matched, allActions, endpointInvocations);
    }

    private static List<RuleActionResult> ParseActions(JsonElement actionsNode)
    {
        if (actionsNode.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var list = new List<RuleActionResult>();
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
                : JsonRuleElements.Parse("{}");

            list.Add(new RuleActionResult(type, parameters));
        }

        return list;
    }

    private static async Task InvokeEndpointCallsAsync(
        Query query,
        ProcedureRuleRecord rule,
        IReadOnlyList<RuleActionResult> actions,
        IRuleEndpointInvoker endpointInvoker,
        List<RuleEndpointInvocationResult> endpointInvocations,
        CancellationToken ct)
    {
        foreach (var action in actions)
        {
            if (!string.Equals(action.Type, "call_endpoint", StringComparison.Ordinal))
            {
                continue;
            }

            var endpointCode = action.Params.TryGetProperty("endpoint_code", out var codeEl)
                ? codeEl.GetString()
                : null;

            if (string.IsNullOrWhiteSpace(endpointCode))
            {
                continue;
            }

            var invocation = await endpointInvoker.InvokeFromRuleAsync(
                query.TenantId,
                endpointCode,
                query.ProcedureInstanceId,
                rule.Id,
                rule.Name,
                ct);

            if (invocation is not null)
            {
                endpointInvocations.Add(invocation);
            }
        }
    }
}
