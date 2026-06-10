using System.Collections.Concurrent;
using Flit.Modules.ProceduresConfig.Domain;
using Flit.Modules.ProceduresConfig.Ports;

namespace Flit.Modules.ProceduresConfig.Adapters;

public sealed class InMemoryOtConsolidatedDocOrderRepository : IOtConsolidatedDocOrderRepository
{
    private readonly ConcurrentDictionary<Guid, OtConsolidatedDocOrderRecord> _orders = new();
    private readonly ConcurrentDictionary<Guid, List<OtConsolidatedDocOrderItemRecord>> _items = new();

    public void SeedOrder(OtConsolidatedDocOrderRecord order, IReadOnlyList<OtConsolidatedDocOrderItemRecord> items)
    {
        _orders[order.Id] = order;
        _items[order.Id] = items.OrderBy(i => i.Position).ToList();
    }

    public Task<(OtConsolidatedDocOrderRecord Order, IReadOnlyList<OtConsolidatedDocOrderItemRecord> Items)?>
        GetActiveOrderWithItemsAsync(Guid trafficAgencyId, CancellationToken ct = default)
    {
        var order = _orders.Values.FirstOrDefault(o =>
            o.TrafficAgencyId == trafficAgencyId && o.IsActive);

        if (order is null)
            return Task.FromResult<(OtConsolidatedDocOrderRecord, IReadOnlyList<OtConsolidatedDocOrderItemRecord>)?>(null);

        if (!_items.TryGetValue(order.Id, out var items))
            items = [];

        return Task.FromResult<(OtConsolidatedDocOrderRecord, IReadOnlyList<OtConsolidatedDocOrderItemRecord>)?>(
            (order, items.OrderBy(i => i.Position).ToList()));
    }
}
