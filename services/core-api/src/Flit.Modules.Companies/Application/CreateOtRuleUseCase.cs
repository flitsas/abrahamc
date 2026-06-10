using Flit.Modules.Companies.Domain;
using Flit.Modules.Companies.Ports;
using Flit.SharedKernel;

namespace Flit.Modules.Companies.Application;

/// <summary>
/// HU #9456 OT-03 — Constructor de reglas de OT.
/// AC1: Crea regla con gatillo, condition_tree y actions válidos.
/// AC2: Rechaza condition_tree o actions malformados con error tipado.
/// </summary>
public static class CreateOtRule
{
    public sealed record Command(
        Guid TrafficAgencyId,
        string Name,
        string TriggerEvent,
        string ConditionTreeJson,
        string ActionsJson,
        int Priority,
        DateTimeOffset? ValidFrom,
        DateTimeOffset? ValidUntil,
        Guid ActorUserId);

    public sealed record Response(Guid RuleId, string Name, bool IsActive);

    public static async Task<Result<Response, string>> HandleAsync(
        Command cmd,
        IOtRuleRepository repo,
        Func<CancellationToken, Task> saveChanges,
        IClock clock,
        CancellationToken ct = default)
    {
        var result = OtRule.Create(
            cmd.TrafficAgencyId,
            cmd.Name,
            cmd.TriggerEvent,
            cmd.ConditionTreeJson,
            cmd.ActionsJson,
            cmd.Priority,
            cmd.ValidFrom,
            cmd.ValidUntil,
            cmd.ActorUserId,
            clock.UtcNow);

        if (!result.IsSuccess)
            return Result<Response, string>.Failure(result.Error);

        var rule = result.Value;
        await repo.AddAsync(rule, ct);
        await saveChanges(ct);

        return Result<Response, string>.Success(new Response(rule.Id, rule.Name, rule.IsActive));
    }
}
