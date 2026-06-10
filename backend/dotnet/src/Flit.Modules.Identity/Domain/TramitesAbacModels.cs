namespace Flit.Modules.Identity.Domain;

/// <summary>Contexto de evaluación ABAC (#9549 RF09-RF10, HU #9684 AC3).</summary>
public sealed record AbacEvaluationContext(
    Guid ActorUserId,
    Guid? ResourceOwnerUserId,
    string? ResourceType,
    DateTimeOffset EvaluatedAt);

public static class TramitesAbacCodes
{
    public const string Denied = "ABAC_DENIED";
}
