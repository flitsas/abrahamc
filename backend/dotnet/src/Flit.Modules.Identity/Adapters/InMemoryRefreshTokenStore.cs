using System.Collections.Concurrent;
using Flit.Modules.Identity.Ports;

namespace Flit.Modules.Identity.Adapters;

/// <summary>
/// Denylist de refresh tokens in-memory (MVP).
/// Produccion: RedisRefreshTokenStore con TTL = refresh_expiry (post-MVP).
/// </summary>
public sealed class InMemoryRefreshTokenStore : IRefreshTokenStore
{
    private readonly ConcurrentDictionary<Guid, (DateTimeOffset Expires, string Reason)> _store = new();

    public Task RevokeAsync(Guid jti, DateTimeOffset expiresAt, string reason, CancellationToken ct)
    {
        _store[jti] = (expiresAt, reason);
        return Task.CompletedTask;
    }

    public Task<bool> IsRevokedAsync(Guid jti, CancellationToken ct)
    {
        if (!_store.TryGetValue(jti, out var entry))
            return Task.FromResult(false);

        // Si expiro, podemos limpiar (best effort, opcional).
        if (entry.Expires < DateTimeOffset.UtcNow)
        {
            _store.TryRemove(jti, out _);
            return Task.FromResult(false);
        }

        return Task.FromResult(true);
    }
}
