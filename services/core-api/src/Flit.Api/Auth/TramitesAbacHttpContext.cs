using Flit.Modules.Identity.Domain;

namespace Flit.Api.Auth;

/// <summary>Contexto ABAC opcional por request (HU #9684 AC3).</summary>
public static class TramitesAbacHttpContext
{
    public const string ItemKey = "flit.abac_context";

    public static void SetAbacContext(HttpContext http, AbacEvaluationContext context) =>
        http.Items[ItemKey] = context;

    public static AbacEvaluationContext? GetAbacContext(HttpContext http) =>
        http.Items.TryGetValue(ItemKey, out var value) ? value as AbacEvaluationContext : null;
}
