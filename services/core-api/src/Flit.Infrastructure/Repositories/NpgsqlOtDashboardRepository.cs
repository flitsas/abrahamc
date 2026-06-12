using Flit.Infrastructure.Persistence;
using Flit.Modules.Companies.Ports;
using Microsoft.EntityFrameworkCore;
using static Flit.Modules.Companies.Application.GetOtDashboard;

namespace Flit.Infrastructure.Repositories;

public sealed class NpgsqlOtDashboardRepository(FlitDbContext db) : IOtDashboardRepository
{
    private sealed class StateCountRow
    {
        public string state { get; init; } = "";
        public int count { get; init; }
    }

    private sealed class RecentRow
    {
        public Guid id { get; init; }
        public string reference_number { get; init; } = "";
        public string state { get; init; } = "";
        public DateTimeOffset? radicated_at { get; init; }
    }

    public async Task<IReadOnlyList<StateCount>> CountByStateAsync(
        Guid trafficAgencyId,
        CancellationToken ct = default)
    {
        var rows = await db.Database.SqlQuery<StateCountRow>($"""
            SELECT state, COUNT(*)::int AS count
            FROM procedures.procedure_instances
            WHERE traffic_agency_id = {trafficAgencyId}
              AND deleted_at IS NULL
            GROUP BY state
            ORDER BY state
            """).ToListAsync(ct);

        return rows.Select(r => new StateCount(r.state, r.count)).ToList();
    }

    public async Task<IReadOnlyList<RecentProcedure>> ListRecentAsync(
        Guid trafficAgencyId,
        int limit,
        CancellationToken ct = default)
    {
        limit = Math.Clamp(limit, 1, 50);
        var rows = await db.Database.SqlQuery<RecentRow>($"""
            SELECT id, reference_number, state, radicated_at
            FROM procedures.procedure_instances
            WHERE traffic_agency_id = {trafficAgencyId}
              AND deleted_at IS NULL
            ORDER BY COALESCE(radicated_at, created_at) DESC
            LIMIT {limit}
            """).ToListAsync(ct);

        return rows.Select(r => new RecentProcedure(
            r.id, r.reference_number, r.state, r.radicated_at)).ToList();
    }
}
