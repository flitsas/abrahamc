using Flit.Modules.Notifications.Adapters.SignalR;

namespace Flit.Modules.Notifications.Ports;

public interface INotificationsPublisher
{
    Task NotifyUserAsync(Guid userId, ProcedureStateChangedNotification n, CancellationToken ct);
    Task NotifyUserAsync(Guid userId, FileUploadedNotification n, CancellationToken ct);
    Task NotifyUserAsync(Guid userId, ReceiptGeneratedNotification n, CancellationToken ct);
    Task NotifyProcedureIdentityGateAsync(
        Guid procedureInstanceId,
        IdentityGateChangedNotification n,
        CancellationToken ct);
}
