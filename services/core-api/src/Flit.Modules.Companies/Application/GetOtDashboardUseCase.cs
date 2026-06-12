using Flit.Modules.Companies.Domain;
using Flit.Modules.Companies.Ports;
using Flit.SharedKernel;

namespace Flit.Modules.Companies.Application;

/// <summary>HU #9697 AC1 — métricas dashboard OT desde BD FLIT (sin Quipux).</summary>
public static class GetOtDashboard
{
    public sealed record StateCount(string State, int Count);

    public sealed record RecentProcedure(
        Guid Id,
        string ReferenceNumber,
        string State,
        DateTimeOffset? RadicatedAt);

    public sealed record Response(
        Guid TrafficAgencyId,
        string IntegrationMode,
        int TotalProcedures,
        IReadOnlyList<StateCount> ByState,
        IReadOnlyList<RecentProcedure> RecentProcedures);

    public static async Task<Result<Response, string>> HandleAsync(
        Guid trafficAgencyId,
        IOtDashboardRepository dashboardRepo,
        IOtQxIntegrationRepository qxRepo,
        CancellationToken ct = default)
    {
        var mode = (await qxRepo.GetByTrafficAgencyAsync(trafficAgencyId, ct))?.Mode
            ?? OtQxIntegration.Modes.Dashboard;

        var byState = await dashboardRepo.CountByStateAsync(trafficAgencyId, ct);
        var recent = await dashboardRepo.ListRecentAsync(trafficAgencyId, 10, ct);
        var total = byState.Sum(x => x.Count);

        return Result<Response, string>.Success(
            new Response(
                trafficAgencyId,
                mode,
                total,
                byState,
                recent));
    }
}
