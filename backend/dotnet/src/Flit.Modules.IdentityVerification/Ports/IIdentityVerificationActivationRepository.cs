namespace Flit.Modules.IdentityVerification.Ports;

using Flit.Modules.IdentityVerification.Domain;
using Flit.Modules.IdentityVerification.Integration;

/// <summary>
/// Persistencia de activacion IDSecure (IDS-02): idempotencia + configs + invitaciones.
/// </summary>
public interface IIdentityVerificationActivationRepository
{
    Task<bool> DomainEventExistsAsync(
        Guid tenantId,
        string idempotencyKey,
        CancellationToken ct);

    Task<IReadOnlyList<ProcedureTypeVerificationConfig>> GetActiveConfigsAsync(
        Guid tenantId,
        Guid procedureTypeId,
        CancellationToken ct);

    Task<IReadOnlyList<VerificationInvitation>> GetInvitationsByInstanceAsync(
        Guid tenantId,
        Guid procedureInstanceId,
        CancellationToken ct);

    Task RegisterDomainEventAsync(VerificationDomainEvent domainEvent, CancellationToken ct);

    Task AddInvitationsAsync(IReadOnlyList<VerificationInvitation> invitations, CancellationToken ct);

    Task SaveChangesAsync(CancellationToken ct);
}

/// <summary>
/// Consumer de TRAMITE_CREATED (stub HTTP DEV / futuro RabbitMQ in-process).
/// </summary>
public interface ITramiteCreatedConsumer
{
    Task ConsumeAsync(TramiteCreatedIntegrationEvent integrationEvent, CancellationToken ct);
}
