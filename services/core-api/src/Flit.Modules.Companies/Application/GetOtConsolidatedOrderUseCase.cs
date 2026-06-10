using Flit.Modules.Companies.Domain;
using Flit.Modules.Companies.Ports;
using Flit.SharedKernel;

namespace Flit.Modules.Companies.Application;

/// <summary>
/// HU #9461 — Obtiene el orden activo del consolidado; lazy-create si no existe (AC2 #9460).
/// </summary>
public static class GetOtConsolidatedOrder
{
    public sealed record Command(Guid TrafficAgencyId, Guid ActorUserId);

    public sealed record Result(Guid OrderId, int Version, IReadOnlyList<OrderItem> Items);

    public sealed record OrderItem(
        Guid Id,
        int Position,
        string Source,
        Guid? ProcedureDocumentCatalogId,
        string? CatalogCode,
        string? CatalogName,
        string? CustomLabel);

    public static async Task<Result<Result, string>> HandleAsync(
        Command cmd,
        IOtConsolidatedOrderRepository repo,
        Func<CancellationToken, Task> saveChanges,
        IClock clock,
        CancellationToken ct = default)
    {
        var readModel = await repo.GetActiveOrderReadModelAsync(cmd.TrafficAgencyId, ct);
        if (readModel is not null)
        {
            return Result<Result, string>.Success(Map(readModel));
        }

        var order = OtConsolidatedDocOrder.Create(cmd.TrafficAgencyId, cmd.ActorUserId, clock.UtcNow);
        await repo.AddOrderAsync(order, ct);
        await saveChanges(ct);

        return Result<Result, string>.Success(new Result(order.Id, order.Version, []));
    }

    private static Result Map(OtConsolidatedOrderReadModel model) =>
        new(
            model.OrderId,
            model.Version,
            model.Items.Select(i => new OrderItem(
                i.Id,
                i.Position,
                i.Source,
                i.ProcedureDocumentCatalogId,
                i.CatalogCode,
                i.CatalogName,
                i.CustomLabel)).ToList());
}
