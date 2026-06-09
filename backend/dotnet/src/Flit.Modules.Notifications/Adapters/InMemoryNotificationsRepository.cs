using System.Collections.Concurrent;
using Flit.Modules.Notifications.Domain;
using Flit.Modules.Notifications.Ports;

namespace Flit.Modules.Notifications.Adapters;

public sealed class InMemoryNotificationsRepository : INotificationsRepository
{
    private readonly ConcurrentDictionary<Guid, NotificationDelivery> _store = new();

    public Task GuardarAsync(NotificationDelivery envio, CancellationToken ct)
    {
        _store[envio.Id] = envio;
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<NotificationDelivery>> ListarPorUserAsync(
        Guid userId, int limit, CancellationToken ct)
    {
        var items = _store.Values
            .Where(n => n.DestinatarioUserId == userId)
            .OrderByDescending(n => n.CreadoEn)
            .Take(limit)
            .ToList();
        return Task.FromResult<IReadOnlyList<NotificationDelivery>>(items);
    }

    public Task<IReadOnlyList<NotificationDelivery>> ListarPorTramiteAsync(
        Guid tramiteId, CancellationToken ct)
    {
        var items = _store.Values
            .Where(n => n.TramiteId == tramiteId)
            .OrderByDescending(n => n.CreadoEn)
            .ToList();
        return Task.FromResult<IReadOnlyList<NotificationDelivery>>(items);
    }
}
