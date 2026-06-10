using Microsoft.EntityFrameworkCore;
using Flit.Infrastructure.Persistence;
using Flit.Modules.Notifications.Domain;
using Flit.Modules.Notifications.Ports;

namespace Flit.Infrastructure.Repositories;

/// <summary>
/// Implementacion EF Core de INotificationsRepository.
/// Tabla notificaciones.notification_deliveriess (ADR-0007).
/// Append-only: GuardarAsync solo inserta; update/delete reservados
/// para el job de limpieza post-cutover.
/// </summary>
public sealed class EfNotificationsRepository(FlitDbContext db)
    : INotificationsRepository
{
    public async Task GuardarAsync(NotificationDelivery envio, CancellationToken ct)
    {
        // Append-only: siempre Add, nunca Update
        db.NotificationDeliveries.Add(envio);
        await db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<NotificationDelivery>> ListarPorUserAsync(
        Guid userId, int limit, CancellationToken ct) =>
        await db.NotificationDeliveries
            .Where(n => n.DestinatarioUserId == userId)
            .OrderByDescending(n => n.CreadoEn)
            .Take(limit)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<NotificationDelivery>> ListarPorTramiteAsync(
        Guid tramiteId, CancellationToken ct) =>
        await db.NotificationDeliveries
            .Where(n => n.TramiteId == tramiteId)
            .OrderByDescending(n => n.CreadoEn)
            .ToListAsync(ct);
}
