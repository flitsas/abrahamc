namespace Flit.Modules.Companies.Ports;

public sealed record IntegrationLogEntry(
    Guid Id,
    Guid TenantId,
    Guid? TrafficAgencyId,
    string Provider,
    string Direction,
    string? EventType,
    string PayloadJson,
    int? HttpStatus,
    string Result,
    int? LatencyMs,
    DateTimeOffset CalledAt);

public interface IIntegrationLogRepository
{
    Task AddAsync(IntegrationLogEntry entry, CancellationToken ct = default);

    Task<(IReadOnlyList<IntegrationLogEntry> Items, int Total)> ListByAgencyAsync(
        Guid tenantId,
        Guid trafficAgencyId,
        int page,
        int pageSize,
        CancellationToken ct = default);
}
