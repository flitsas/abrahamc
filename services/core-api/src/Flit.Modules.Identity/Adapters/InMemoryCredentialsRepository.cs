using System.Collections.Concurrent;
using Flit.Modules.Identity.Ports;

namespace Flit.Modules.Identity.Adapters;

public sealed class InMemoryCredentialsRepository : ICredentialsRepository
{
    private readonly ConcurrentDictionary<Guid, (string Hash, DateTimeOffset ChangedAt)> _store = new();

    public Task<string?> ObtenerPasswordHashAsync(Guid userId, CancellationToken ct)
    {
        _store.TryGetValue(userId, out var v);
        return Task.FromResult<string?>(v.Hash);
    }

    public Task GuardarPasswordHashAsync(
        Guid userId, string passwordHash, DateTimeOffset cambiadoEn, CancellationToken ct)
    {
        _store[userId] = (passwordHash, cambiadoEn);
        return Task.CompletedTask;
    }

    public Task EliminarAsync(Guid userId, CancellationToken ct)
    {
        _store.TryRemove(userId, out _);
        return Task.CompletedTask;
    }
}
