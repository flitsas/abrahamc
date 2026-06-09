using System.Collections.Concurrent;

namespace Flit.Modules.Integrations.Application;

/// <summary>Circuit breaker por tenant+conector (INT-01 #9431).</summary>
public sealed class ExternalQueryCircuitBreaker
{
    public const int FailureThreshold = 3;
    public static readonly TimeSpan FailureWindow = TimeSpan.FromSeconds(30);
    public static readonly TimeSpan OpenDuration = TimeSpan.FromSeconds(60);

    private readonly ConcurrentDictionary<string, CircuitState> _states = new();

    public bool IsOpen(Guid tenantId, string connectorCode)
    {
        var key = BuildKey(tenantId, connectorCode);
        if (!_states.TryGetValue(key, out var state))
        {
            return false;
        }

        lock (state.Lock)
        {
            if (state.OpenUntil is { } until && DateTimeOffset.UtcNow < until)
            {
                return true;
            }

            if (state.OpenUntil is not null && DateTimeOffset.UtcNow >= state.OpenUntil)
            {
                state.OpenUntil = null;
                state.FailureTimestamps.Clear();
            }

            return false;
        }
    }

    public void RecordSuccess(Guid tenantId, string connectorCode)
    {
        var key = BuildKey(tenantId, connectorCode);
        var state = _states.GetOrAdd(key, _ => new CircuitState());
        lock (state.Lock)
        {
            state.FailureTimestamps.Clear();
            state.OpenUntil = null;
        }
    }

    public void RecordFailure(Guid tenantId, string connectorCode)
    {
        var key = BuildKey(tenantId, connectorCode);
        var state = _states.GetOrAdd(key, _ => new CircuitState());
        var now = DateTimeOffset.UtcNow;

        lock (state.Lock)
        {
            state.FailureTimestamps.RemoveAll(t => now - t > FailureWindow);
            state.FailureTimestamps.Add(now);

            if (state.FailureTimestamps.Count >= FailureThreshold)
            {
                state.OpenUntil = now.Add(OpenDuration);
            }
        }
    }

    private static string BuildKey(Guid tenantId, string connectorCode) =>
        $"{tenantId:N}:{connectorCode.Trim().ToUpperInvariant()}";

    private sealed class CircuitState
    {
        public object Lock { get; } = new();
        public List<DateTimeOffset> FailureTimestamps { get; } = [];
        public DateTimeOffset? OpenUntil { get; set; }
    }
}
