using System.Collections.Concurrent;
using Flit.Modules.IdentityVerification.Ports;

namespace Flit.Modules.IdentityVerification.Adapters;

/// <summary>
/// Captura notificaciones de desbloqueo para tests/DEV (HU #9486).
/// </summary>
public sealed class CaptureProcedureIdentityAdvanceNotifier : IProcedureIdentityAdvanceNotifier
{
    private readonly ConcurrentQueue<ProcedureIdentityUnblockedNotification> _notifications = new();

    public IReadOnlyList<ProcedureIdentityUnblockedNotification> Notifications =>
        _notifications.ToList();

    public Task NotifyUnblockedAsync(
        ProcedureIdentityUnblockedNotification notification,
        CancellationToken ct = default)
    {
        _notifications.Enqueue(notification);
        return Task.CompletedTask;
    }

    public void Clear() => _notifications.Clear();
}
