using Flit.Modules.IdentityVerification.Application;
using Flit.Modules.IdentityVerification.Integration;
using Flit.Modules.IdentityVerification.Ports;
using Flit.SharedKernel;

namespace Flit.Modules.IdentityVerification.Adapters;

/// <summary>
/// Adaptador in-process del consumer TRAMITE_CREATED (HTTP DEV o futuro bus).
/// Tras crear invitaciones, dispara envio SMTP HTML (HU #9480).
/// </summary>
public sealed class TramiteCreatedConsumer(
    IIdentityVerificationActivationRepository activationRepo,
    IVerificationInvitationRepository invitationRepo,
    IInvitationTokenGenerator tokenGenerator,
    IInvitationEmailSender emailSender,
    IIdSecurePublicUrlProvider urlProvider,
    IClock clock,
    ITenantContext tenantContext)
    : ITramiteCreatedConsumer
{
    public async Task ConsumeAsync(TramiteCreatedIntegrationEvent integrationEvent, CancellationToken ct)
    {
        tenantContext.SetTenant(integrationEvent.TenantId);

        var result = await HandleTramiteCreated.HandleAsync(
            new HandleTramiteCreated.Command(integrationEvent),
            activationRepo,
            tokenGenerator,
            clock,
            ct);

        if (!result.IsSuccess)
            throw new InvalidOperationException(
                $"{result.Error.Code}: {result.Error.Message}");

        if (result.Value.WasDuplicate)
            return;

        foreach (var created in result.Value.CreatedInvitations)
        {
            var sendResult = await SendInvitationEmail.HandleAsync(
                new SendInvitationEmail.Command(
                    integrationEvent.TenantId,
                    created.InvitationId,
                    created.PlainToken,
                    created.EmailTemplateKey,
                    integrationEvent.ProcedureTypeId,
                    integrationEvent.ActorUserId),
                invitationRepo,
                emailSender,
                urlProvider,
                clock,
                ct);

            if (!sendResult.IsSuccess)
                throw new InvalidOperationException(
                    $"{sendResult.Error.Code}: {sendResult.Error.Message}");
        }
    }
}
