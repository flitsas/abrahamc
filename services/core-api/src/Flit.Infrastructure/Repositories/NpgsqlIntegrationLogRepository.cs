using Flit.Infrastructure.Persistence;
using Flit.Modules.Companies.Ports;
using Microsoft.EntityFrameworkCore;

namespace Flit.Infrastructure.Repositories;

public sealed class NpgsqlIntegrationLogRepository(FlitDbContext db) : IIntegrationLogRepository
{
    private sealed class CountRow
    {
        public int count { get; init; }
    }

    private sealed class LogRow
    {
        public Guid id { get; init; }
        public Guid tenant_id { get; init; }
        public Guid? traffic_agency_id { get; init; }
        public string provider { get; init; } = "";
        public string direction { get; init; } = "";
        public string? event_type { get; init; }
        public string payload { get; init; } = "{}";
        public int? http_status { get; init; }
        public string result { get; init; } = "";
        public int? latency_ms { get; init; }
        public DateTimeOffset called_at { get; init; }
    }

    public async Task AddAsync(IntegrationLogEntry entry, CancellationToken ct = default)
    {
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO integrations.integration_logs (
              id, tenant_id, traffic_agency_id, provider, direction, event_type,
              payload, http_status, result, latency_ms, called_at, created_by)
            VALUES (
              {entry.Id}, {entry.TenantId}, {entry.TrafficAgencyId}, {entry.Provider},
              {entry.Direction}, {entry.EventType}, {entry.PayloadJson}::jsonb,
              {entry.HttpStatus}, {entry.Result}, {entry.LatencyMs}, {entry.CalledAt},
              '00000000-0000-7000-8000-000000000001')
            """, ct);
    }

    public async Task<(IReadOnlyList<IntegrationLogEntry> Items, int Total)> ListByAgencyAsync(
        Guid tenantId,
        Guid trafficAgencyId,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var countRows = await db.Database.SqlQuery<CountRow>($"""
            SELECT COUNT(*)::int AS count
            FROM integrations.integration_logs
            WHERE tenant_id = {tenantId}
              AND traffic_agency_id = {trafficAgencyId}
            """).ToListAsync(ct);
        var total = countRows.FirstOrDefault()?.count ?? 0;

        var rows = await db.Database.SqlQuery<LogRow>($"""
            SELECT id, tenant_id, traffic_agency_id, provider, direction, event_type,
                   payload::text AS payload, http_status, result, latency_ms, called_at
            FROM integrations.integration_logs
            WHERE tenant_id = {tenantId}
              AND traffic_agency_id = {trafficAgencyId}
            ORDER BY called_at DESC
            OFFSET {(page - 1) * pageSize} LIMIT {pageSize}
            """).ToListAsync(ct);

        var items = rows.Select(r => new IntegrationLogEntry(
            r.id, r.tenant_id, r.traffic_agency_id, r.provider, r.direction,
            r.event_type, r.payload, r.http_status, r.result, r.latency_ms, r.called_at)).ToList();

        return (items, total);
    }
}
