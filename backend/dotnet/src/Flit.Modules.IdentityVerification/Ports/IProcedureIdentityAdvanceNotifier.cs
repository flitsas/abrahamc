namespace Flit.Modules.IdentityVerification.Ports;

/// <summary>
/// Notificación al motor de trámites cuando el gate de identidad se desbloquea (HU #9486).
/// </summary>
public interface IProcedureIdentityAdvanceNotifier
{
    Task NotifyUnblockedAsync(
        ProcedureIdentityUnblockedNotification notification,
        CancellationToken ct = default);
}

public sealed record ProcedureIdentityUnblockedNotification(
    Guid TenantId,
    Guid ProcedureInstanceId,
    int ApprovedParticipantCount,
    DateTimeOffset OccurredAt);
