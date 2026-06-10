using Flit.Modules.Procedures.Domain;
using Flit.Modules.ProceduresConfig.Ports;

namespace Flit.Modules.Procedures.Adapters;

public sealed class ProcedureInstanceExistsAdapter(IProcedureInstanceRepository repository)
    : IProcedureInstanceExistsPort
{
    public async Task<bool> ExistsAsync(
        Guid tenantId,
        Guid procedureInstanceId,
        CancellationToken ct = default) =>
        await repository.GetByIdAsync(procedureInstanceId, tenantId, ct) is not null;
}
