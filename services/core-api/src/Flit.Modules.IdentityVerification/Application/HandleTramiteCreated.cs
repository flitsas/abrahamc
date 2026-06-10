using Flit.Modules.IdentityVerification.Domain;
using Flit.Modules.IdentityVerification.Integration;
using Flit.Modules.IdentityVerification.Ports;
using Flit.SharedKernel;

namespace Flit.Modules.IdentityVerification.Application;

/// <summary>
/// HU #9479 — Consumer TRAMITE_CREATED con idempotencia via verification_domain_events.
/// </summary>
public static class HandleTramiteCreated
{
    public static readonly TimeSpan InvitationTtl = TimeSpan.FromHours(48);

    public sealed record Command(TramiteCreatedIntegrationEvent Event);

    public sealed record Response(
        bool WasDuplicate,
        IReadOnlyList<Guid> InvitationIds,
        IReadOnlyList<CreatedInvitation> CreatedInvitations);

    public sealed record CreatedInvitation(
        Guid InvitationId,
        string PlainToken,
        string EmailTemplateKey);

    public abstract record HandleTramiteCreatedError(string Code, string Message)
    {
        public sealed record ValidationFailed(string Detail)
            : HandleTramiteCreatedError("VALIDATION_FAILED", Detail);

        public sealed record MissingRequiredParticipant(string ParticipantRole)
            : HandleTramiteCreatedError(
                "MISSING_REQUIRED_PARTICIPANT",
                $"Participante requerido sin email en el evento: {ParticipantRole}");
    }

    public static async Task<Result<Response, HandleTramiteCreatedError>> HandleAsync(
        Command cmd,
        IIdentityVerificationActivationRepository repo,
        IInvitationTokenGenerator tokenGenerator,
        IClock clock,
        CancellationToken ct = default)
    {
        var evt = cmd.Event;
        var validation = Validate(evt);
        if (validation is not null)
            return Result<Response, HandleTramiteCreatedError>.Failure(validation);

        if (await repo.DomainEventExistsAsync(evt.TenantId, evt.IdempotencyKey, ct))
        {
            var existing = await repo.GetInvitationsByInstanceAsync(
                evt.TenantId, evt.ProcedureInstanceId, ct);
            return Result<Response, HandleTramiteCreatedError>.Success(
                new Response(WasDuplicate: true, existing.Select(i => i.Id).ToList(), []));
        }

        var configs = await repo.GetActiveConfigsAsync(
            evt.TenantId, evt.ProcedureTypeId, ct);

        var participantsByRole = evt.Participants
            .GroupBy(p => p.ParticipantRole.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        var invitations = new List<VerificationInvitation>();
        var createdWithTokens = new List<CreatedInvitation>();
        var now = clock.UtcNow;
        var expiresAt = now.Add(InvitationTtl);

        foreach (var config in configs.OrderBy(c => c.SortOrder))
        {
            if (!config.IsRequired || !config.IsActive)
                continue;

            if (!participantsByRole.TryGetValue(config.ParticipantRole, out var participant)
                || string.IsNullOrWhiteSpace(participant.Email))
            {
                return Result<Response, HandleTramiteCreatedError>.Failure(
                    new HandleTramiteCreatedError.MissingRequiredParticipant(config.ParticipantRole));
            }

            var generated = tokenGenerator.Generate();

            var invitation = VerificationInvitation.CreatePending(
                tenantId: evt.TenantId,
                procedureInstanceId: evt.ProcedureInstanceId,
                procedureActorId: participant.ProcedureActorId,
                participantRole: config.ParticipantRole,
                recipientEmail: participant.Email,
                tokenHash: generated.TokenHash,
                expiresAt: expiresAt,
                idempotencyKey: evt.IdempotencyKey,
                actorUserId: evt.ActorUserId,
                now: now);

            invitations.Add(invitation);
            createdWithTokens.Add(new CreatedInvitation(
                invitation.Id,
                generated.PlainToken,
                config.EmailTemplateKey));
        }

        var payloadJson =
            $$"""{"procedureInstanceId":"{{evt.ProcedureInstanceId}}","procedureTypeId":"{{evt.ProcedureTypeId}}","idempotencyKey":"{{evt.IdempotencyKey}}","invitationCount":{{invitations.Count}}}""";

        var domainEvent = VerificationDomainEvent.CreateTramiteCreated(
            evt.TenantId,
            evt.ProcedureInstanceId,
            evt.IdempotencyKey,
            payloadJson,
            now);

        await repo.RegisterDomainEventAsync(domainEvent, ct);
        if (invitations.Count > 0)
            await repo.AddInvitationsAsync(invitations, ct);

        try
        {
            await repo.SaveChangesAsync(ct);
        }
        catch (DuplicateDomainEventException)
        {
            var existing = await repo.GetInvitationsByInstanceAsync(
                evt.TenantId, evt.ProcedureInstanceId, ct);
            return Result<Response, HandleTramiteCreatedError>.Success(
                new Response(WasDuplicate: true, existing.Select(i => i.Id).ToList(), []));
        }

        return Result<Response, HandleTramiteCreatedError>.Success(
            new Response(WasDuplicate: false, invitations.Select(i => i.Id).ToList(), createdWithTokens));
    }

    private static HandleTramiteCreatedError.ValidationFailed? Validate(TramiteCreatedIntegrationEvent evt)
    {
        if (evt.TenantId == Guid.Empty)
            return new HandleTramiteCreatedError.ValidationFailed("TenantId requerido");
        if (evt.ProcedureInstanceId == Guid.Empty)
            return new HandleTramiteCreatedError.ValidationFailed("ProcedureInstanceId requerido");
        if (evt.ProcedureTypeId == Guid.Empty)
            return new HandleTramiteCreatedError.ValidationFailed("ProcedureTypeId requerido");
        if (string.IsNullOrWhiteSpace(evt.IdempotencyKey))
            return new HandleTramiteCreatedError.ValidationFailed("IdempotencyKey requerido");
        if (evt.ActorUserId == Guid.Empty)
            return new HandleTramiteCreatedError.ValidationFailed("ActorUserId requerido");
        return null;
    }
}

/// <summary>Señal para idempotencia concurrente (unique uq_verification_domain_events_idempotency).</summary>
public sealed class DuplicateDomainEventException : Exception
{
    public DuplicateDomainEventException()
        : base("TRAMITE_CREATED ya procesado para este idempotency_key") { }
}
