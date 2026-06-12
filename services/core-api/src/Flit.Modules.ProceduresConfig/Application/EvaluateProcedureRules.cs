using System.Text.Json;
using Flit.Modules.ProceduresConfig.Ports;

namespace Flit.Modules.ProceduresConfig.Application;

/// <summary>
/// RGL-02 (#9438): evalúa reglas por prioridad; hot-swap (solo activas en BD); snapshot para radicados (AC2).
/// HU #9694 AC4: persiste bitácora en rule_execution_logs en evaluación real.
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
        IRuleExecutionLogRepository executionLogRepo,
        CancellationToken ct = default)
    {
        var matched = await ProcedureRulesMatcher.MatchAsync(
            query.TenantId,
            query.ProcedureTypeId,
            query.CapturedFields,
            query.ConfigSnapshot,
            rulesRepo,
            ct);

        var allActions = matched.SelectMany(m => m.Actions).ToList();
        var endpointInvocations = new List<RuleEndpointInvocationResult>();

        foreach (var ruleMatch in matched)
        {
            await InvokeEndpointCallsAsync(
                query,
                ruleMatch.RuleId,
                ruleMatch.RuleName,
                ruleMatch.Actions,
                endpointInvoker,
                endpointInvocations,
                ct);
        }

        var response = new Response(matched, allActions, endpointInvocations);

        await LogExecutionAsync(query, response, executionLogRepo, ct);

        return response;
    }

    private static async Task LogExecutionAsync(
        Query query,
        Response response,
        IRuleExecutionLogRepository executionLogRepo,
        CancellationToken ct)
    {
        var payload = JsonSerializer.SerializeToElement(new
        {
            capturedFields = query.CapturedFields,
            usedSnapshot = query.ConfigSnapshot is not null,
        });

        var matchedRules = JsonSerializer.SerializeToElement(
            response.MatchedRules.Select(m => new
            {
                ruleId = m.RuleId,
                ruleName = m.RuleName,
                priority = m.Priority,
                actions = m.Actions.Select(a => new { type = a.Type, @params = a.Params }),
            }));

        var result = JsonSerializer.SerializeToElement(new
        {
            actions = response.Actions.Select(a => new { type = a.Type, @params = a.Params }),
            endpointInvocations = response.EndpointInvocations.Select(e => new
            {
                endpointCode = e.EndpointCode,
                httpStatus = e.HttpStatus,
                succeeded = e.Succeeded,
                rateLimited = e.RateLimited,
            }),
        });

        await executionLogRepo.LogAsync(
            new RuleExecutionLogEntry(
                query.TenantId,
                query.ProcedureTypeId,
                query.ProcedureInstanceId,
                payload,
                matchedRules,
                result,
                DateTimeOffset.UtcNow),
            ct);
    }

    private static async Task InvokeEndpointCallsAsync(
        Query query,
        Guid ruleId,
        string ruleName,
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
                ruleId,
                ruleName,
                ct);

            if (invocation is not null)
            {
                endpointInvocations.Add(invocation);
            }
        }
    }
}
