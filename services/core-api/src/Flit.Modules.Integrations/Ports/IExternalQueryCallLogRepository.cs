using System.Text.Json;

namespace Flit.Modules.Integrations.Ports;

public sealed record ExternalQueryCallLogEntry(
    Guid Id,
    Guid TenantId,
    Guid? ProcedureInstanceId,
    string QueryConnectorCode,
    string? EdgeRole,
    JsonElement Request,
    JsonElement? Response,
    int? HttpStatus,
    int? LatencyMs,
    bool Succeeded,
    string? ErrorMessage,
    DateTimeOffset CalledAt);

/// <summary>Bitácora <c>integrations.external_query_calls</c> (CF-I1 / AC1 #9431).</summary>
public interface IExternalQueryCallLogRepository
{
    Task<ExternalQueryCallLogEntry?> FindCompletedByIdempotencyKeyAsync(
        Guid tenantId,
        string queryConnectorCode,
        string idempotencyKey,
        CancellationToken ct = default);

    Task LogAsync(ExternalQueryCallLogEntry entry, CancellationToken ct = default);
}
