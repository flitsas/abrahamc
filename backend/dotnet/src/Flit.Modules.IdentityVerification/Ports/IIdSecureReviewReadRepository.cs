using Flit.Modules.IdentityVerification.Domain;

namespace Flit.Modules.IdentityVerification.Ports;

/// <summary>
/// Lecturas agregadas para panel backoffice IDSecure (HU #9487).
/// </summary>
public interface IIdSecureReviewReadRepository
{
    Task<IReadOnlyList<VerificationSession>> ListSessionsByTenantAsync(
        Guid tenantId,
        CancellationToken ct);

    Task<VerificationInvitation?> FindInvitationByIdAsync(
        Guid tenantId,
        Guid invitationId,
        CancellationToken ct);

    Task<VerificationDomainEvent?> FindDomainEventByInstanceAsync(
        Guid tenantId,
        Guid procedureInstanceId,
        CancellationToken ct);
}
