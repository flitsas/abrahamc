using Flit.Modules.Procedures.Domain;
using Flit.Modules.ProceduresConfig.Ports;

namespace Flit.Modules.Procedures.Adapters;

public sealed class ProcedureInstanceTrafficAgencyAdapter(IProcedureInstanceRepository repository)
    : IProcedureInstanceTrafficAgencyPort
{
    public async Task<Guid?> GetTrafficAgencyIdAsync(
        Guid tenantId,
        Guid procedureInstanceId,
        CancellationToken ct = default)
    {
        var instance = await repository.GetByIdAsync(procedureInstanceId, tenantId, ct);
        return instance?.TrafficAgencyId;
    }
}
