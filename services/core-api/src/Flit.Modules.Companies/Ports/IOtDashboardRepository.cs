using static Flit.Modules.Companies.Application.GetOtDashboard;

namespace Flit.Modules.Companies.Ports;

/// <summary>Agregados de dashboard OT (HU #9697).</summary>
public interface IOtDashboardRepository
{
    Task<IReadOnlyList<StateCount>> CountByStateAsync(
        Guid trafficAgencyId,
        CancellationToken ct = default);

    Task<IReadOnlyList<RecentProcedure>> ListRecentAsync(
        Guid trafficAgencyId,
        int limit,
        CancellationToken ct = default);
}
