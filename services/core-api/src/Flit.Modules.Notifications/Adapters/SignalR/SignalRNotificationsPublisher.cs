using Flit.Modules.Notifications.Ports;
using Microsoft.AspNetCore.SignalR;

namespace Flit.Modules.Notifications.Adapters.SignalR;

public sealed class SignalRNotificationsPublisher : INotificationsPublisher
{
    private readonly IHubContext<FlitNotificationsHub, IFlitNotificationsClient> _hub;

    public SignalRNotificationsPublisher(IHubContext<FlitNotificationsHub, IFlitNotificationsClient> hub)
        => _hub = hub;

    public Task NotifyUserAsync(Guid userId, ProcedureStateChangedNotification n, CancellationToken ct)
        => _hub.Clients.Group($"user:{userId}").ProcedureStateChanged(n);

    public Task NotifyUserAsync(Guid userId, FileUploadedNotification n, CancellationToken ct)
        => _hub.Clients.Group($"user:{userId}").FileUploaded(n);

    public Task NotifyUserAsync(Guid userId, ReceiptGeneratedNotification n, CancellationToken ct)
        => _hub.Clients.Group($"user:{userId}").ReceiptGenerated(n);

    public Task NotifyProcedureIdentityGateAsync(
        Guid procedureInstanceId,
        IdentityGateChangedNotification n,
        CancellationToken ct)
        => _hub.Clients.Group($"procedure:{procedureInstanceId}").IdentityGateChanged(n);
}
