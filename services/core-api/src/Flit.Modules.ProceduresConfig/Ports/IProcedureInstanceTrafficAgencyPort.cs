namespace Flit.Modules.ProceduresConfig.Ports;

/// <summary>Resuelve el OT de una instancia radicada (DOC-02 #9443).</summary>
public interface IProcedureInstanceTrafficAgencyPort
{
    Task<Guid?> GetTrafficAgencyIdAsync(
        Guid tenantId,
        Guid procedureInstanceId,
        CancellationToken ct = default);
}
