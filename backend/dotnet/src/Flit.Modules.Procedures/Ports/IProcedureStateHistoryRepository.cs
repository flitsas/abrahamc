using Flit.Modules.Procedures.Domain;

namespace Flit.Modules.Procedures.Ports;

/// <summary>Historial de transiciones de estado (TRA-01 #9433).</summary>
public interface IProcedureStateHistoryRepository
{
    Task AppendAsync(ProcedureStateHistoryEntry entry, CancellationToken ct = default);

    Task<IReadOnlyList<ProcedureStateHistoryEntry>> ListByInstanceAsync(
        Guid tenantId,
        Guid procedureInstanceId,
        CancellationToken ct = default);
}
