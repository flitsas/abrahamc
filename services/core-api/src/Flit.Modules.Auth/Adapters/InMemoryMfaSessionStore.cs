using System.Collections.Concurrent;
using Flit.Modules.Auth.Ports;

namespace Flit.Modules.Auth.Adapters;

/// <summary>
/// InMemory store de sesiones MFA pendientes para desarrollo local.
/// TTL implementado con timestamp + cleanup en cada lookup (lazy expiration).
/// Para multi-instance prod usar RedisMfaSessionStore.
///
/// Limites por defecto: 3 intentos antes de DELETE forzar relogin.
/// </summary>
public sealed class InMemoryMfaSessionStore : IMfaSessionStore
{
    private readonly ConcurrentDictionary<Guid, Entry> _sessions = new();

    private sealed record Entry(PendingMfaSession Session, DateTimeOffset ExpiresAt);

    public Task<Guid> CreateAsync(PendingMfaSession session, TimeSpan ttl, CancellationToken ct = default)
    {
        var sessionId = Guid.CreateVersion7();
        var expiresAt = DateTimeOffset.UtcNow.Add(ttl);
        _sessions[sessionId] = new Entry(session, expiresAt);
        return Task.FromResult(sessionId);
    }

    public Task<PendingMfaSession?> GetAsync(Guid sessionId, CancellationToken ct = default)
    {
        if (!_sessions.TryGetValue(sessionId, out var entry))
            return Task.FromResult<PendingMfaSession?>(null);

        if (DateTimeOffset.UtcNow > entry.ExpiresAt)
        {
            _sessions.TryRemove(sessionId, out _);
            return Task.FromResult<PendingMfaSession?>(null);
        }

        return Task.FromResult<PendingMfaSession?>(entry.Session);
    }

    public Task<int> IncrementAttemptsAsync(Guid sessionId, CancellationToken ct = default)
    {
        if (!_sessions.TryGetValue(sessionId, out var entry))
            return Task.FromResult(-1);

        var updated = entry.Session with { Attempts = entry.Session.Attempts + 1 };
        _sessions[sessionId] = entry with { Session = updated };
        return Task.FromResult(updated.Attempts);
    }

    public Task DeleteAsync(Guid sessionId, CancellationToken ct = default)
    {
        _sessions.TryRemove(sessionId, out _);
        return Task.CompletedTask;
    }
}
