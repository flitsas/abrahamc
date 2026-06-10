using System.Text.Json;
using Flit.Modules.Identity.Domain;

namespace Flit.Modules.Identity.Application;

/// <summary>Evalúa condiciones ABAC en identity.role_permissions.abac_conditions (#9684).</summary>
public static class TramitesAbacEvaluator
{
    public static bool IsGranted(IReadOnlyList<string?> conditionJsonList, AbacEvaluationContext ctx)
    {
        if (conditionJsonList.Count == 0)
            return false;

        foreach (var json in conditionJsonList)
        {
            if (string.IsNullOrWhiteSpace(json))
                return true;

            if (EvaluateJson(json, ctx))
                return true;
        }

        return false;
    }

    public static bool EvaluateJson(string json, AbacEvaluationContext ctx)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            return Evaluate(doc.RootElement, ctx);
        }
        catch (JsonException)
        {
            return false;
        }
    }

    public static bool Evaluate(JsonElement root, AbacEvaluationContext ctx)
    {
        if (root.TryGetProperty("resource", out var resourceProp))
        {
            var expected = resourceProp.GetString();
            if (!string.IsNullOrEmpty(expected) &&
                !string.Equals(expected, ctx.ResourceType, StringComparison.OrdinalIgnoreCase))
                return false;
        }

        if (root.TryGetProperty("constraint", out var constraintProp) &&
            string.Equals(constraintProp.GetString(), "own_only", StringComparison.Ordinal))
        {
            if (!ctx.ResourceOwnerUserId.HasValue || ctx.ResourceOwnerUserId.Value != ctx.ActorUserId)
                return false;
        }

        return true;
    }
}
