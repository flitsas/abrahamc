using System.Collections.Concurrent;
using Flit.Modules.Companies.Ports;

namespace Flit.Infrastructure.Integrations;

/// <summary>Circuit breaker en memoria (DEV / monolito). Umbral: 3 fallos → abierto 60s.</summary>
public sealed class InMemoryRuntProviderCircuitBreaker : IRuntProviderCircuitBreaker
{
    private const int FailureThreshold = 3;
    private static readonly TimeSpan OpenDuration = TimeSpan.FromSeconds(60);

    private readonly ConcurrentDictionary<string, CircuitState> _states = new();

    public bool IsOpen(Guid tenantId, string providerCode, DateTimeOffset now)
    {
        var key = Key(tenantId, providerCode);
        if (!_states.TryGetValue(key, out var state))
            return false;

        if (state.OpenUntil is { } until)
        {
            if (until > now)
                return true;
            _states.TryRemove(key, out _);
        }

        return false;
    }

    public void RecordFailure(Guid tenantId, string providerCode, DateTimeOffset now)
    {
        var key = Key(tenantId, providerCode);
        _states.AddOrUpdate(
            key,
            _ => new CircuitState(1, null),
            (_, existing) =>
            {
                var failures = existing.Failures + 1;
                var openUntil = failures >= FailureThreshold ? now.Add(OpenDuration) : existing.OpenUntil;
                return new CircuitState(failures, openUntil);
            });
    }

    public void RecordSuccess(Guid tenantId, string providerCode)
    {
        _states.TryRemove(Key(tenantId, providerCode), out _);
    }

    private static string Key(Guid tenantId, string providerCode) => $"{tenantId:N}:{providerCode}";

    private sealed record CircuitState(int Failures, DateTimeOffset? OpenUntil);
}
