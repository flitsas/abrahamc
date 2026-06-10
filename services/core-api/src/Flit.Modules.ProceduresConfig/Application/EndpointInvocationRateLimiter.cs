using System.Collections.Concurrent;

namespace Flit.Modules.ProceduresConfig.Application;

/// <summary>Rate limit por tenant+código (AC1 #9439): ventana fija en memoria.</summary>
public sealed class EndpointInvocationRateLimiter
{
    public const int DefaultMaxCallsPerMinute = 30;

    private readonly ConcurrentDictionary<string, RateWindow> _windows = new();
    private readonly int _maxPerMinute;

    public EndpointInvocationRateLimiter(int maxPerMinute = DefaultMaxCallsPerMinute)
    {
        _maxPerMinute = Math.Max(1, maxPerMinute);
    }

    public bool TryAcquire(Guid tenantId, string endpointCode)
    {
        var key = $"{tenantId:N}:{endpointCode.Trim().ToUpperInvariant()}";
        var now = DateTimeOffset.UtcNow;
        var window = _windows.AddOrUpdate(
            key,
            _ => new RateWindow(now, 1),
            (_, existing) => existing.TryIncrement(now, _maxPerMinute));

        return window.Count <= _maxPerMinute;
    }

    private sealed class RateWindow
    {
        private readonly object _lock = new();
        public DateTimeOffset WindowStart { get; private set; }
        public int Count { get; private set; }

        public RateWindow(DateTimeOffset start, int count)
        {
            WindowStart = start;
            Count = count;
        }

        public RateWindow TryIncrement(DateTimeOffset now, int max)
        {
            lock (_lock)
            {
                if (now - WindowStart >= TimeSpan.FromMinutes(1))
                {
                    WindowStart = now;
                    Count = 1;
                    return this;
                }

                Count++;
                return this;
            }
        }
    }
}
