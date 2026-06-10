using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Flit.Modules.Notifications.Adapters.SignalR;

/// <summary>
/// Hub SignalR para notificaciones push en tiempo real al frontend.
/// Reemplaza el módulo WebSockets de services/node-bff/ (eliminado por ADR-0014).
///
/// El frontend se conecta a /hubs/notifications (proxeado por Flit.Gateway/YARP).
/// Grupos por user_id permiten dirigir notificaciones a usuarios específicos.
/// </summary>
[AllowAnonymous]
public sealed class FlitNotificationsHub : Hub<IFlitNotificationsClient>
{
    /// <summary>HU #9488 — suscripción al semáforo IDSecure del detalle de trámite.</summary>
    public Task JoinProcedureGroup(string procedureInstanceId)
    {
        if (!Guid.TryParse(procedureInstanceId, out _))
            throw new HubException("procedureInstanceId inválido");

        return Groups.AddToGroupAsync(Context.ConnectionId, $"procedure:{procedureInstanceId}");
    }

    public override async Task OnConnectedAsync()
    {
        var userId = Context.UserIdentifier;
        if (!string.IsNullOrEmpty(userId))
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"user:{userId}");
        }
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var userId = Context.UserIdentifier;
        if (!string.IsNullOrEmpty(userId))
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"user:{userId}");
        }
        await base.OnDisconnectedAsync(exception);
    }
}

public interface IFlitNotificationsClient
{
    Task ProcedureStateChanged(ProcedureStateChangedNotification notification);
    Task FileUploaded(FileUploadedNotification notification);
    Task ReceiptGenerated(ReceiptGeneratedNotification notification);
    Task IdentityGateChanged(IdentityGateChangedNotification notification);
}

public sealed record IdentityGateChangedNotification(
    Guid ProcedureInstanceId,
    int RequiredParticipantCount,
    int ApprovedCount,
    bool IsBlocked,
    bool CanAdvance,
    DateTimeOffset At);

public sealed record ProcedureStateChangedNotification(
    Guid ProcedureId,
    string ProcedureCode,
    string NewStatus,
    DateTimeOffset At);

public sealed record FileUploadedNotification(
    Guid FileId,
    string Filename,
    string Scope,
    Guid ScopeId,
    DateTimeOffset At);

public sealed record ReceiptGeneratedNotification(
    Guid ProcedureId,
    Guid ReceiptFileId,
    DateTimeOffset At);
