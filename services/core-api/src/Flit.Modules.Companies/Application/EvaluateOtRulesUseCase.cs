using System.Text.Json;
using Flit.Modules.Companies.Domain;
using Flit.Modules.Companies.Ports;
using Flit.Modules.ProceduresConfig.Domain;
using Flit.SharedKernel;

namespace Flit.Modules.Companies.Application;

/// <summary>HU #9699 — evalúa reglas OT activas en on_submit.</summary>
public static class EvaluateOtRules
{
    public sealed record BlockResult(string RuleName, string Message);

    public static async Task<Result<IReadOnlyList<BlockResult>, string>> HandleAsync(
        Guid trafficAgencyId,
        string triggerEvent,
        IReadOnlyDictionary<string, string?> capturedFields,
        IOtRuleRepository rulesRepo,
        IClock clock,
        CancellationToken ct = default)
    {
        var rules = await rulesRepo.ListActiveByTriggerAsync(trafficAgencyId, triggerEvent, ct);
        var now = clock.UtcNow;
        var blocks = new List<BlockResult>();

        foreach (var rule in rules)
        {
            if (!rule.IsActive || rule.IsDeleted)
                continue;

            if (rule.ValidFrom is not null && now < rule.ValidFrom)
                continue;
            if (rule.ValidUntil is not null && now > rule.ValidUntil)
                continue;

            using var condDoc = JsonDocument.Parse(rule.ConditionTreeJson);
            if (!RuleConditionEvaluator.Evaluate(condDoc.RootElement, capturedFields))
                continue;

            using var actDoc = JsonDocument.Parse(rule.ActionsJson);
            if (actDoc.RootElement.ValueKind != JsonValueKind.Array)
                continue;

            foreach (var action in actDoc.RootElement.EnumerateArray())
            {
                if (!action.TryGetProperty("type", out var typeEl))
                    continue;

                if (!string.Equals(typeEl.GetString(), "block", StringComparison.OrdinalIgnoreCase))
                    continue;

                var message = action.TryGetProperty("message", out var msgEl)
                    ? msgEl.GetString() ?? "Trámite bloqueado por regla OT."
                    : "Trámite bloqueado por regla OT.";

                blocks.Add(new BlockResult(rule.Name, message));
            }
        }

        return Result<IReadOnlyList<BlockResult>, string>.Success(blocks);
    }
}
