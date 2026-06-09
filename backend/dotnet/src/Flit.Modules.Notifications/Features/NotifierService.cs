using Flit.Modules.Notifications.Domain;
using Flit.Modules.Notifications.Ports;
using Flit.SharedKernel;

namespace Flit.Modules.Notifications.Features;

/// <summary>
/// Encola notificaciones genéricas (email/SMS/push) sin acoplar al dominio legacy de procedures.
/// </summary>
public sealed class NotifierService(
    INotificationsRepository repo,
    INotificationDispatcher dispatcher,
    IClock clock)
{
    public async Task<NotificationDelivery> EnqueueAsync(
        TipoNotificacion type,
        CanalNotificacion channel,
        string recipient,
        string recipientMasked,
        Guid? destinatarioUserId,
        Guid? tramiteId,
        string? subject,
        string body,
        CancellationToken ct = default)
    {
        var delivery = new NotificationDelivery(
            Id: Guid.CreateVersion7(),
            Tipo: type,
            Canal: channel,
            DestinatarioUserId: destinatarioUserId,
            DestinatarioContacto: recipientMasked,
            TramiteId: tramiteId,
            Estado: EstadoEnvio.Pendiente,
            Asunto: subject,
            CuerpoResumen: Truncate(body, 200),
            ErrorMensaje: null,
            CreadoEn: clock.UtcNow,
            EnviadoEn: null);

        await repo.GuardarAsync(delivery, ct);

        await dispatcher.DispatchAsync(
            new NotificationDispatchEvent(
                DeliveryId: delivery.Id,
                Channel: channel,
                Recipient: recipient,
                Subject: subject,
                Body: body,
                Metadata: BuildMetadata(tramiteId, destinatarioUserId)),
            ct);

        return delivery;
    }

    private static Dictionary<string, string> BuildMetadata(Guid? tramiteId, Guid? destinatarioUserId)
    {
        var meta = new Dictionary<string, string>();
        if (tramiteId is not null) meta["procedure_id"] = tramiteId.Value.ToString();
        if (destinatarioUserId is not null) meta["user_id"] = destinatarioUserId.Value.ToString();
        return meta;
    }

    private static string Truncate(string s, int max)
        => string.IsNullOrEmpty(s) ? string.Empty
           : (s.Length <= max ? s : s[..max] + "...");
}
