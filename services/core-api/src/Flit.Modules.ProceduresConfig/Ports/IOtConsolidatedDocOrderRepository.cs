using Flit.Modules.ProceduresConfig.Domain;

namespace Flit.Modules.ProceduresConfig.Ports;

/// <summary>Lectura del orden consolidado parametrizado por OT (DOC-02 #9443).</summary>
public interface IOtConsolidatedDocOrderRepository
{
    Task<(OtConsolidatedDocOrderRecord Order, IReadOnlyList<OtConsolidatedDocOrderItemRecord> Items)?>
        GetActiveOrderWithItemsAsync(Guid trafficAgencyId, CancellationToken ct = default);
}
