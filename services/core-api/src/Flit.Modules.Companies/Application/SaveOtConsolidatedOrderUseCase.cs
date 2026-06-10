using Flit.Modules.Companies.Domain;
using Flit.Modules.Companies.Ports;
using Flit.SharedKernel;

namespace Flit.Modules.Companies.Application;

/// <summary>
/// HU #9461 AC1 — Reemplaza ítems y positions en transacción de dos fases (&lt;500ms).
/// </summary>
public static class SaveOtConsolidatedOrder
{
    public sealed record Command(
        Guid TrafficAgencyId,
        Guid ActorUserId,
        IReadOnlyList<SaveConsolidatedOrderItemCommand> Items);

    public static async Task<Result<bool, string>> HandleAsync(
        Command cmd,
        IOtConsolidatedOrderRepository repo,
        Func<CancellationToken, Task> saveChanges,
        IClock clock,
        CancellationToken ct = default)
    {
        if (cmd.Items.Count == 0)
        {
            var emptyOrder = await repo.GetActiveOrderAsync(cmd.TrafficAgencyId, ct);
            if (emptyOrder is null)
            {
                var created = OtConsolidatedDocOrder.Create(cmd.TrafficAgencyId, cmd.ActorUserId, clock.UtcNow);
                await repo.AddOrderAsync(created, ct);
                await saveChanges(ct);
                return Result<bool, string>.Success(true);
            }

            await repo.ReplaceItemsAsync(
                cmd.TrafficAgencyId, emptyOrder.Id, [], cmd.ActorUserId, clock.UtcNow, ct);
            await saveChanges(ct);
            return Result<bool, string>.Success(true);
        }

        var validationError = ValidateItems(cmd.Items);
        if (validationError is not null)
            return Result<bool, string>.Failure(validationError);

        foreach (var item in cmd.Items.Where(i =>
                     string.Equals(i.Source, OtConsolidatedDocOrderItem.Sources.Global, StringComparison.OrdinalIgnoreCase)))
        {
            if (item.ProcedureDocumentCatalogId is null
                || !await repo.CatalogEntryExistsAsync(item.ProcedureDocumentCatalogId.Value, ct))
            {
                return Result<bool, string>.Failure(
                    $"Catálogo de documento inválido: {item.ProcedureDocumentCatalogId}.");
            }
        }

        var order = await repo.GetActiveOrderAsync(cmd.TrafficAgencyId, ct);
        if (order is null)
        {
            order = OtConsolidatedDocOrder.Create(cmd.TrafficAgencyId, cmd.ActorUserId, clock.UtcNow);
            await repo.AddOrderAsync(order, ct);
            await saveChanges(ct);
        }

        await repo.ReplaceItemsAsync(
            cmd.TrafficAgencyId, order.Id, cmd.Items, cmd.ActorUserId, clock.UtcNow, ct);
        await saveChanges(ct);

        return Result<bool, string>.Success(true);
    }

    public static string? ValidateItems(IReadOnlyList<SaveConsolidatedOrderItemCommand> items)
    {
        var positions = items.Select(i => i.Position).ToList();
        if (positions.Any(p => p < 1))
            return "Cada position debe ser >= 1.";

        if (positions.Distinct().Count() != positions.Count)
            return "Las positions deben ser únicas.";

        var expected = Enumerable.Range(1, items.Count).ToHashSet();
        if (!positions.ToHashSet().SetEquals(expected))
            return "Las positions deben ser consecutivas desde 1.";

        foreach (var item in items)
        {
            if (!OtConsolidatedDocOrderItem.Sources.All.Contains(item.Source))
                return $"source '{item.Source}' inválido.";

            if (string.Equals(item.Source, OtConsolidatedDocOrderItem.Sources.Global, StringComparison.OrdinalIgnoreCase))
            {
                if (item.ProcedureDocumentCatalogId is null || item.ProcedureDocumentCatalogId == Guid.Empty)
                    return "procedure_document_catalog_id es requerido para source global.";
                if (!string.IsNullOrWhiteSpace(item.CustomLabel))
                    return "custom_label no aplica para source global.";
            }
            else if (string.IsNullOrWhiteSpace(item.CustomLabel))
            {
                return "custom_label es requerido para source custom.";
            }
        }

        var duplicateCatalog = items
            .Where(i => i.ProcedureDocumentCatalogId.HasValue)
            .GroupBy(i => i.ProcedureDocumentCatalogId!.Value)
            .Any(g => g.Count() > 1);
        if (duplicateCatalog)
            return "No se puede repetir el mismo documento del catálogo en el orden.";

        return null;
    }
}
