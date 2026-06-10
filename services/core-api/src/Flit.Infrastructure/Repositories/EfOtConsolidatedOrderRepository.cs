using Flit.Infrastructure.Persistence;
using Flit.Modules.Companies.Domain;
using Flit.Modules.Companies.Ports;
using Microsoft.EntityFrameworkCore;

namespace Flit.Infrastructure.Repositories;

/// <summary>
/// Repositorio EF Core para orden consolidado OT (HU #9461 — Feature #9379).
/// </summary>
public sealed class EfOtConsolidatedOrderRepository(FlitDbContext db) : IOtConsolidatedOrderRepository
{
    private const int PositionOffset = 100_000;

    private sealed class OrderItemRow
    {
        public Guid order_id { get; init; }
        public int version { get; init; }
        public Guid? item_id { get; init; }
        public int? position { get; init; }
        public string? source { get; init; }
        public Guid? procedure_document_catalog_id { get; init; }
        public string? catalog_code { get; init; }
        public string? catalog_name { get; init; }
        public string? custom_label { get; init; }
    }

    public Task<OtConsolidatedDocOrder?> GetActiveOrderAsync(
        Guid trafficAgencyId,
        CancellationToken ct = default) =>
        db.OtConsolidatedDocOrders
            .FirstOrDefaultAsync(
                x => x.TrafficAgencyId == trafficAgencyId && x.IsActive && x.DeletedAt == null,
                ct);

    public async Task<OtConsolidatedOrderReadModel?> GetActiveOrderReadModelAsync(
        Guid trafficAgencyId,
        CancellationToken ct = default)
    {
        var rows = await db.Database
            .SqlQuery<OrderItemRow>($"""
                SELECT
                    o.id          AS order_id,
                    o.version     AS version,
                    i.id          AS item_id,
                    i.position    AS position,
                    i.source      AS source,
                    i.procedure_document_catalog_id,
                    c.code        AS catalog_code,
                    c.name        AS catalog_name,
                    i.custom_label
                FROM ot.ot_consolidated_doc_orders o
                LEFT JOIN ot.ot_consolidated_doc_order_items i ON i.order_id = o.id
                LEFT JOIN procedures_config.procedure_document_catalog c
                    ON c.id = i.procedure_document_catalog_id AND c.is_active = true
                WHERE o.traffic_agency_id = {trafficAgencyId}
                  AND o.is_active = true
                  AND o.deleted_at IS NULL
                ORDER BY i.position NULLS LAST
                """)
            .ToListAsync(ct);

        if (rows.Count == 0)
            return null;

        var head = rows[0];
        var items = rows
            .Where(r => r.item_id.HasValue)
            .Select(r => new OtConsolidatedOrderItemReadModel(
                r.item_id!.Value,
                r.position ?? 0,
                r.source ?? OtConsolidatedDocOrderItem.Sources.Global,
                r.procedure_document_catalog_id,
                r.catalog_code,
                r.catalog_name,
                r.custom_label))
            .ToList();

        return new OtConsolidatedOrderReadModel(head.order_id, head.version, items);
    }

    public async Task AddOrderAsync(OtConsolidatedDocOrder order, CancellationToken ct = default) =>
        await db.OtConsolidatedDocOrders.AddAsync(order, ct);

    private sealed class CatalogExistsRow
    {
        public Guid id { get; init; }
    }

    public async Task<bool> CatalogEntryExistsAsync(Guid catalogId, CancellationToken ct = default)
    {
        var rows = await db.Database
            .SqlQuery<CatalogExistsRow>($"""
                SELECT id
                FROM procedures_config.procedure_document_catalog
                WHERE id = {catalogId} AND is_active = true
                LIMIT 1
                """)
            .ToListAsync(ct);
        return rows.Count > 0;
    }

    public async Task ReplaceItemsAsync(
        Guid trafficAgencyId,
        Guid orderId,
        IReadOnlyList<SaveConsolidatedOrderItemCommand> items,
        Guid actorUserId,
        DateTimeOffset now,
        CancellationToken ct = default)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);

        var existing = await db.OtConsolidatedDocOrderItems
            .Where(x => x.OrderId == orderId && x.TrafficAgencyId == trafficAgencyId)
            .ToListAsync(ct);

        await db.Database.ExecuteSqlInterpolatedAsync(
            $"""
            UPDATE ot.ot_consolidated_doc_order_items
            SET position = position + {PositionOffset},
                updated_at = {now},
                updated_by = {actorUserId},
                row_version = row_version + 1
            WHERE order_id = {orderId}
            """,
            ct);

        var keepIds = items
            .Where(i => i.Id.HasValue)
            .Select(i => i.Id!.Value)
            .ToHashSet();

        var toRemove = existing.Where(e => !keepIds.Contains(e.Id)).ToList();
        if (toRemove.Count > 0)
            db.OtConsolidatedDocOrderItems.RemoveRange(toRemove);

        foreach (var cmd in items)
        {
            if (cmd.Id.HasValue)
            {
                var entity = existing.FirstOrDefault(e => e.Id == cmd.Id.Value);
                if (entity is null)
                    continue;

                entity.SetPosition(cmd.Position + PositionOffset, actorUserId, now);
                continue;
            }

            var createResult = string.Equals(
                cmd.Source,
                OtConsolidatedDocOrderItem.Sources.Global,
                StringComparison.OrdinalIgnoreCase)
                ? OtConsolidatedDocOrderItem.CreateGlobal(
                    trafficAgencyId,
                    orderId,
                    cmd.ProcedureDocumentCatalogId!.Value,
                    cmd.Position + PositionOffset,
                    actorUserId,
                    now)
                : OtConsolidatedDocOrderItem.CreateCustom(
                    trafficAgencyId,
                    orderId,
                    cmd.CustomLabel!,
                    cmd.Position + PositionOffset,
                    actorUserId,
                    now);

            if (createResult.IsSuccess)
                await db.OtConsolidatedDocOrderItems.AddAsync(createResult.Value, ct);
        }

        await db.SaveChangesAsync(ct);

        foreach (var cmd in items)
        {
            if (cmd.Id.HasValue)
            {
                await db.Database.ExecuteSqlInterpolatedAsync(
                    $"""
                    UPDATE ot.ot_consolidated_doc_order_items
                    SET position = {cmd.Position},
                        updated_at = {now},
                        updated_by = {actorUserId},
                        row_version = row_version + 1
                    WHERE id = {cmd.Id.Value} AND order_id = {orderId}
                    """,
                    ct);
            }
            else
            {
                await db.Database.ExecuteSqlInterpolatedAsync(
                    $"""
                    UPDATE ot.ot_consolidated_doc_order_items
                    SET position = {cmd.Position},
                        updated_at = {now},
                        updated_by = {actorUserId},
                        row_version = row_version + 1
                    WHERE order_id = {orderId}
                      AND position = {cmd.Position + PositionOffset}
                    """,
                    ct);
            }
        }

        await TouchOrderAsync(orderId, actorUserId, now, ct);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }

    public async Task<bool> RemoveItemAsync(
        Guid trafficAgencyId,
        Guid orderId,
        Guid itemId,
        Guid actorUserId,
        DateTimeOffset now,
        CancellationToken ct = default)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);

        var entity = await db.OtConsolidatedDocOrderItems
            .FirstOrDefaultAsync(
                x => x.Id == itemId && x.OrderId == orderId && x.TrafficAgencyId == trafficAgencyId,
                ct);
        if (entity is null)
            return false;

        db.OtConsolidatedDocOrderItems.Remove(entity);
        await db.SaveChangesAsync(ct);

        var remaining = await db.OtConsolidatedDocOrderItems
            .Where(x => x.OrderId == orderId)
            .OrderBy(x => x.Position)
            .ToListAsync(ct);

        await db.Database.ExecuteSqlInterpolatedAsync(
            $"""
            UPDATE ot.ot_consolidated_doc_order_items
            SET position = position + {PositionOffset},
                updated_at = {now},
                updated_by = {actorUserId},
                row_version = row_version + 1
            WHERE order_id = {orderId}
            """,
            ct);

        for (var i = 0; i < remaining.Count; i++)
        {
            var pos = i + 1;
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"""
                UPDATE ot.ot_consolidated_doc_order_items
                SET position = {pos},
                    updated_at = {now},
                    updated_by = {actorUserId},
                    row_version = row_version + 1
                WHERE id = {remaining[i].Id}
                """,
                ct);
        }

        await TouchOrderAsync(orderId, actorUserId, now, ct);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return true;
    }

    public async Task TouchOrderAsync(
        Guid orderId,
        Guid actorUserId,
        DateTimeOffset now,
        CancellationToken ct = default)
    {
        var order = await db.OtConsolidatedDocOrders.FirstOrDefaultAsync(x => x.Id == orderId, ct);
        if (order is null)
            return;

        order.Touch(actorUserId, now);
        db.OtConsolidatedDocOrders.Update(order);
    }
}
