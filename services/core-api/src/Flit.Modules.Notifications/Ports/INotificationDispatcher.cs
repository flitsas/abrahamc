using Flit.Modules.Notifications.Domain;

namespace Flit.Modules.Notifications.Ports;

/// <summary>
/// Puerto para despachar la EJECUCION del envio de la notificacion.
///
/// Post ADR-0014: core-api es DUENO COMPLETO de Notifications (dominio + ejecucion).
/// La ejecucion externa (SMTP/SMS/push) la hace un consumer RabbitMQ dentro de
/// Flit.Modules.Notifications, no un servicio aparte. SignalR Hub empuja
/// el resultado al frontend en tiempo real.
///
/// Implementaciones:
///   - StubNotificationDispatcher (dev local sin bus): solo loguea, marca como Enviado
///   - RabbitMqNotificationDispatcher (prod, pendiente Fase 9): publica a exchange
///     flit.notifications con routing key notification.dispatch; consumer in-process
///     ejecuta el envio externo.
/// </summary>
public interface INotificationDispatcher
{
    /// <summary>
    /// Publica el evento de despacho al bus. NO bloquea esperando confirmacion
    /// de envio — eso llega por evento de retorno (notification.delivered |
    /// notification.failed) que el dominio consume aparte.
    /// </summary>
    Task DispatchAsync(NotificationDispatchEvent evt, CancellationToken ct = default);
}

/// <summary>
/// Evento publicado al bus para que el consumer in-process (Flit.Modules.Notifications,
/// post ADR-0014) ejecute el envio externo. Idempotente por DeliveryId.
/// </summary>
public sealed record NotificationDispatchEvent(
    Guid DeliveryId,
    CanalNotificacion Channel,
    string Recipient,
    string? Subject,
    string Body,
    IReadOnlyDictionary<string, string> Metadata);
