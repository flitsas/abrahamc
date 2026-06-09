using Flit.Modules.IdentityVerification.Application;
using Flit.Modules.IdentityVerification.Ports;
using Flit.Modules.Notifications.Adapters.SignalR;
using Flit.Modules.Notifications.Ports;
using Flit.SharedKernel;

namespace Flit.Api.Endpoints;

/// <summary>Publica cambios del gate IDSecure vía SignalR (HU #9488).</summary>
internal static class IdentityGateNotificationHelper
{
    internal static async Task NotifyIfPossibleAsync(
        Guid tenantId,
        Guid procedureInstanceId,
        IIdentityVerificationActivationRepository activationRepo,
        IVerificationSessionRepository sessionRepo,
        INotificationsPublisher publisher,
        IClock clock,
        CancellationToken ct)
    {
        var gateResult = await ProcedureIdentityGate.GetStatusAsync(
            tenantId,
            procedureInstanceId,
            activationRepo,
            sessionRepo,
            ct);

        if (gateResult.IsSuccess)
            await PublishAsync(gateResult.Value, publisher, clock, ct);
    }

    internal static Task PublishAsync(
        ProcedureIdentityGate.GateStatusResponse gate,
        INotificationsPublisher publisher,
        IClock clock,
        CancellationToken ct)
    {
        var notification = new IdentityGateChangedNotification(
            gate.ProcedureInstanceId,
            gate.RequiredParticipantCount,
            gate.ApprovedCount,
            gate.IsBlocked,
            gate.CanAdvance,
            clock.UtcNow);

        return publisher.NotifyProcedureIdentityGateAsync(
            gate.ProcedureInstanceId,
            notification,
            ct);
    }
}
