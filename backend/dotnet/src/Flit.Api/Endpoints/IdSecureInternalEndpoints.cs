using Flit.Modules.IdentityVerification.Application;
using Flit.Modules.IdentityVerification.Integration;
using Flit.Modules.IdentityVerification.Ports;
using Flit.Modules.Notifications.Ports;
using Flit.SharedKernel;
using MailKit.Security;

namespace Flit.Api.Endpoints;

/// <summary>
/// Endpoints internos IDSecure (stub DEV consumer TRAMITE_CREATED — HU #9479/#9480).
/// </summary>
public static class IdSecureInternalEndpoints
{
    public sealed record TramiteCreatedRequest(
        Guid TenantId,
        Guid ProcedureInstanceId,
        Guid ProcedureTypeId,
        string IdempotencyKey,
        IReadOnlyList<TramiteParticipantRequest> Participants,
        Guid ActorUserId);

    public sealed record TramiteParticipantRequest(
        Guid? ProcedureActorId,
        string ParticipantRole,
        string Email);

    public sealed record TramiteCreatedResponse(
        bool WasDuplicate,
        IReadOnlyList<Guid> InvitationIds);

    public static void MapIdSecureInternalEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/internal/idsecure")
            .WithTags("IDSecure - Internal");

        group.MapPost("/events/tramite-created", async (
            TramiteCreatedRequest body,
            IIdentityVerificationActivationRepository activationRepo,
            IVerificationInvitationRepository invitationRepo,
            IInvitationTokenGenerator tokenGenerator,
            IInvitationEmailSender emailSender,
            IIdSecurePublicUrlProvider urlProvider,
            IClock clock,
            ITenantContext tenantContext,
            CancellationToken ct) =>
        {
            tenantContext.SetTenant(body.TenantId);

            var integrationEvent = new TramiteCreatedIntegrationEvent(
                body.TenantId,
                body.ProcedureInstanceId,
                body.ProcedureTypeId,
                body.IdempotencyKey,
                body.Participants
                    .Select(p => new TramiteParticipantInfo(
                        p.ProcedureActorId,
                        p.ParticipantRole,
                        p.Email))
                    .ToList(),
                body.ActorUserId);

            var result = await HandleTramiteCreated.HandleAsync(
                new HandleTramiteCreated.Command(integrationEvent),
                activationRepo,
                tokenGenerator,
                clock,
                ct);

            return await result.Match<Task<IResult>>(
                onSuccess: async response =>
                {
                    if (response.WasDuplicate)
                    {
                        return Results.Conflict(new TramiteCreatedResponse(
                            response.WasDuplicate,
                            response.InvitationIds));
                    }

                    foreach (var created in response.CreatedInvitations)
                    {
                        try
                        {
                            var sendResult = await SendInvitationEmail.HandleAsync(
                                new SendInvitationEmail.Command(
                                    body.TenantId,
                                    created.InvitationId,
                                    created.PlainToken,
                                    created.EmailTemplateKey,
                                    body.ProcedureTypeId,
                                    body.ActorUserId),
                                invitationRepo,
                                emailSender,
                                urlProvider,
                                clock,
                                ct);

                            if (!sendResult.IsSuccess)
                            {
                                return Results.Problem(
                                    detail: sendResult.Error.Message,
                                    statusCode: StatusCodes.Status502BadGateway,
                                    title: sendResult.Error.Code);
                            }
                        }
                        catch (AuthenticationException ex)
                        {
                            return Results.Problem(
                                detail: "Autenticación SMTP rechazada por Office365. "
                                        + "Verifique Smtp__User/Smtp__Password (contraseña de aplicación si hay MFA) "
                                        + "y que SMTP AUTH esté habilitado para la cuenta.",
                                statusCode: StatusCodes.Status502BadGateway,
                                title: "SMTP_AUTH_FAILED",
                                extensions: new Dictionary<string, object?> { ["smtpDetail"] = ex.Message });
                        }
                    }

                    return Results.Created(
                        $"/internal/idsecure/instances/{body.ProcedureInstanceId}/invitations",
                        new TramiteCreatedResponse(response.WasDuplicate, response.InvitationIds));
                },
                onFailure: err => Task.FromResult<IResult>(
                    Results.BadRequest(new { err.Code, err.Message })));
        })
        .WithName("IdSecureTramiteCreated");

        group.MapGet("/instances/{procedureInstanceId:guid}/identity-gate", async (
            Guid procedureInstanceId,
            Guid tenantId,
            IIdentityVerificationActivationRepository activationRepo,
            IVerificationSessionRepository sessionRepo,
            ITenantContext tenantContext,
            CancellationToken ct) =>
        {
            tenantContext.SetTenant(tenantId);

            var result = await ProcedureIdentityGate.GetStatusAsync(
                tenantId,
                procedureInstanceId,
                activationRepo,
                sessionRepo,
                ct);

            return result.Match<IResult>(
                onSuccess: r => Results.Ok(r),
                onFailure: err => Results.Problem(
                    detail: err.Message,
                    statusCode: err.HttpStatus,
                    title: err.Code));
        })
        .WithName("IdSecureProcedureIdentityGate");
    }

    public static void MapProcedureIdentityWebhookEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/internal/procedures/{procedureInstanceId:guid}/identity-status", async (
            Guid procedureInstanceId,
            Guid tenantId,
            IIdentityVerificationActivationRepository activationRepo,
            IVerificationSessionRepository sessionRepo,
            IVerificationInvitationRepository invitationRepo,
            IProcedureIdentityValidationRepository validationRepo,
            IProcedureIdentityAdvanceNotifier advanceNotifier,
            INotificationsPublisher notificationsPublisher,
            IClock clock,
            ITenantContext tenantContext,
            CancellationToken ct) =>
        {
            tenantContext.SetTenant(tenantId);

            var result = await ProcedureIdentityGate.ProcessIdentityStatusWebhookAsync(
                tenantId,
                procedureInstanceId,
                activationRepo,
                sessionRepo,
                invitationRepo,
                validationRepo,
                advanceNotifier,
                clock,
                ct);

            if (result.IsSuccess)
            {
                var gateResult = await ProcedureIdentityGate.GetStatusAsync(
                    tenantId,
                    procedureInstanceId,
                    activationRepo,
                    sessionRepo,
                    ct);
                if (gateResult.IsSuccess)
                {
                    await IdentityGateNotificationHelper.PublishAsync(
                        gateResult.Value,
                        notificationsPublisher,
                        clock,
                        ct);
                }
            }

            return result.Match<IResult>(
                onSuccess: r => Results.Ok(r),
                onFailure: err => Results.Problem(
                    detail: err.Message,
                    statusCode: err.HttpStatus,
                    title: err.Code));
        })
        .WithTags("Procedures - Internal")
        .WithName("ProcedureIdentityStatusWebhook");
    }
}
