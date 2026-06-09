using Flit.Modules.Notifications.Ports;

namespace Flit.Modules.Notifications.Adapters;

/// <summary>
/// Adapter de desarrollo: serializa el evento a Console, no publica al bus.
/// Usar cuando RABBITMQ_URL no esta configurado (dev local sin Docker).
///
/// En produccion sustituir por RabbitMqNotificationDispatcher (Fase 9 backlog).
/// </summary>
public sealed class StubNotificationDispatcher : INotificationDispatcher
{
    public Task DispatchAsync(NotificationDispatchEvent evt, CancellationToken ct = default)
    {
        Console.WriteLine(
            $"[StubDispatcher] DeliveryId={evt.DeliveryId} Channel={evt.Channel} " +
            $"Recipient={evt.Recipient} Subject={evt.Subject ?? "(none)"} " +
            $"Body.Length={evt.Body.Length} Metadata.Count={evt.Metadata.Count}");
        return Task.CompletedTask;
    }
}
