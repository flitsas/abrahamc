using Flit.Modules.ProceduresConfig.Domain;
using Flit.Modules.ProceduresConfig.Ports;

namespace Flit.Modules.ProceduresConfig.Application;

public static class ListActiveProcedureTypes
{
    public sealed record Query(Guid TenantId, Guid? TrafficAgencyId);

    public static Task<IReadOnlyList<ProcedureTypeListItem>> HandleAsync(
        Query query,
        IProceduresConfigReadRepository repo,
        CancellationToken ct = default) =>
        repo.ListEffectiveActiveTypesAsync(query.TenantId, query.TrafficAgencyId, ct);
}

public static class GetProcedureConfiguration
{
    public sealed record Query(Guid TenantId, string ProcedureTypeCode, Guid? TrafficAgencyId);

    public static async Task<ResolvedProcedureConfiguration?> HandleAsync(
        Query query,
        IProceduresConfigReadRepository repo,
        CancellationToken ct = default)
    {
        var bundle = await repo.LoadConfigurationBundleAsync(
            query.TenantId,
            query.ProcedureTypeCode,
            query.TrafficAgencyId,
            ct);

        return bundle is null ? null : ProcedureConfigurationResolver.Resolve(bundle);
    }
}
