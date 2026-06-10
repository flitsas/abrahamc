using Flit.Modules.Companies.Ports;
using Flit.SharedKernel;

namespace Flit.Modules.Companies.Application;

/// <summary>
/// HU #9461 AC2 — Quita un ítem del orden (borra fila; no toca archivos en MinIO).
/// </summary>
public static class RemoveOtConsolidatedOrderItem
{
    public sealed record Command(Guid TrafficAgencyId, Guid ItemId, Guid ActorUserId);

    public enum ErrorCode
    {
        NotFound,
    }

    public sealed record RemoveItemError(ErrorCode Code, string Message);

    public static async Task<Result<bool, RemoveItemError>> HandleAsync(
        Command cmd,
        IOtConsolidatedOrderRepository repo,
        Func<CancellationToken, Task> saveChanges,
        IClock clock,
        CancellationToken ct = default)
    {
        var order = await repo.GetActiveOrderAsync(cmd.TrafficAgencyId, ct);
        if (order is null)
        {
            return Result<bool, RemoveItemError>.Failure(
                new RemoveItemError(ErrorCode.NotFound, "Orden activo no encontrado."));
        }

        var removed = await repo.RemoveItemAsync(
            cmd.TrafficAgencyId, order.Id, cmd.ItemId, cmd.ActorUserId, clock.UtcNow, ct);
        if (!removed)
        {
            return Result<bool, RemoveItemError>.Failure(
                new RemoveItemError(ErrorCode.NotFound, "Ítem no encontrado en el orden activo."));
        }

        await saveChanges(ct);
        return Result<bool, RemoveItemError>.Success(true);
    }
}
