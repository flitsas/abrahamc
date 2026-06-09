using Flit.Modules.ProceduresConfig.Domain;

namespace Flit.Modules.ProceduresConfig.Ports;

public interface IProceduresConfigReadRepository
{
    Task<IReadOnlyList<ProcedureTypeListItem>> ListEffectiveActiveTypesAsync(
        Guid tenantId,
        Guid? trafficAgencyId,
        CancellationToken ct = default);

    Task<ProcedureConfigurationBundle?> LoadConfigurationBundleAsync(
        Guid tenantId,
        string procedureTypeCode,
        Guid? trafficAgencyId,
        CancellationToken ct = default);
}
