using Flit.Modules.Companies.Ports;
using Flit.SharedKernel;

namespace Flit.Modules.Companies.Application;

/// <summary>
/// HU #9456 OT-03 — Toggle hot-swap de regla OT.
/// AC1: Cuando is_active se cambia, la regla evalúa inmediatamente sobre trámites del OT (hot-swap).
/// </summary>
public static class ToggleOtRule
{
    public enum ErrorCode { NotFound }

    public sealed record Command(Guid RuleId, Guid TrafficAgencyId, Guid ActorUserId);

    public sealed record Response(Guid RuleId, bool IsActive);

    public sealed record ToggleError(ErrorCode Code, string Message);

    public static async Task<Result<Response, ToggleError>> HandleAsync(
        Command cmd,
        IOtRuleRepository repo,
        Func<CancellationToken, Task> saveChanges,
        IClock clock,
        CancellationToken ct = default)
    {
        var rule = await repo.GetByIdAsync(cmd.RuleId, cmd.TrafficAgencyId, ct);
        if (rule is null || rule.IsDeleted)
            return Result<Response, ToggleError>.Failure(
                new ToggleError(ErrorCode.NotFound,
                    $"Regla {cmd.RuleId} no encontrada para el OT."));

        rule.Toggle(cmd.ActorUserId, clock.UtcNow);
        await repo.UpdateAsync(rule, ct);
        await saveChanges(ct);

        return Result<Response, ToggleError>.Success(new Response(rule.Id, rule.IsActive));
    }
}
