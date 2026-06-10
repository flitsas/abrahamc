using Flit.Modules.Notifications.Domain;

namespace Flit.Modules.Notifications.Ports;

public interface INotificationsRepository
{
    Task GuardarAsync(NotificationDelivery envio, CancellationToken ct);
    Task<IReadOnlyList<NotificationDelivery>> ListarPorUserAsync(
        Guid userId, int limit, CancellationToken ct);
    Task<IReadOnlyList<NotificationDelivery>> ListarPorTramiteAsync(
        Guid tramiteId, CancellationToken ct);
}
