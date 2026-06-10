using System.Collections.Concurrent;
using Flit.Modules.Integrations.Domain;
using Flit.Modules.Integrations.Ports;

namespace Flit.Modules.Integrations.Adapters;

public sealed class InMemoryExternalQueryCallLogRepository : IExternalQueryCallLogRepository
{
    private readonly ConcurrentDictionary<string, ExternalQueryCallLogEntry> _byIdempotency = new();

    public Task<ExternalQueryCallLogEntry?> FindCompletedByIdempotencyKeyAsync(
        Guid tenantId,
        string queryConnectorCode,
        string idempotencyKey,
        CancellationToken ct = default)
    {
        var key = BuildKey(tenantId, queryConnectorCode, idempotencyKey);
        _byIdempotency.TryGetValue(key, out var entry);
        return Task.FromResult(entry);
    }

    public Task LogAsync(ExternalQueryCallLogEntry entry, CancellationToken ct = default)
    {
        var idemKey = ExternalQueryPayloadMetadata.TryGetIdempotencyKey(entry.Request);
        if (string.IsNullOrWhiteSpace(idemKey))
        {
            return Task.CompletedTask;
        }

        var key = BuildKey(entry.TenantId, entry.QueryConnectorCode, idemKey);
        if (entry.Succeeded)
        {
            _byIdempotency[key] = entry;
        }

        return Task.CompletedTask;
    }

    public IReadOnlyList<ExternalQueryCallLogEntry> GetAllEntries() =>
        _byIdempotency.Values.ToList();

    private static string BuildKey(Guid tenantId, string connector, string idempotencyKey) =>
        $"{tenantId:N}:{connector.Trim().ToUpperInvariant()}:{idempotencyKey}";
}
