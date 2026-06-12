using Flit.Modules.Companies.Domain;
using Flit.Modules.Companies.Ports;
using Flit.SharedKernel;

namespace Flit.Modules.Companies.Application;

/// <summary>HU #9697 AC2-AC3 — hot-swap modo Dashboard/QX sin reiniciar servicio.</summary>
public static class UpdateOtIntegrationMode
{
    public sealed record Command(Guid TrafficAgencyId, string Mode, Guid ActorUserId);

    public sealed record Response(Guid TrafficAgencyId, string Mode);

    public static async Task<Result<Response, string>> HandleAsync(
        Command cmd,
        IOtQxIntegrationRepository qxRepo,
        Func<CancellationToken, Task> saveChanges,
        IClock clock,
        CancellationToken ct = default)
    {
        var validation = OtQxIntegration.ValidateMode(cmd.Mode);
        if (validation is not null)
            return Result<Response, string>.Failure(validation);

        var normalized = OtQxIntegration.NormalizeMode(cmd.Mode);
        var updated = await qxRepo.UpsertModeAsync(
            cmd.TrafficAgencyId,
            normalized,
            cmd.ActorUserId,
            clock.UtcNow,
            ct);

        await saveChanges(ct);

        var apiMode = string.Equals(updated.Mode, OtQxIntegration.Modes.Qx, StringComparison.OrdinalIgnoreCase)
            ? "quipux"
            : updated.Mode;

        return Result<Response, string>.Success(
            new Response(cmd.TrafficAgencyId, apiMode));
    }
}
