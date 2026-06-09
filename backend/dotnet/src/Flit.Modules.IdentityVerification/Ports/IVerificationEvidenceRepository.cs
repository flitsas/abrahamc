using Flit.Modules.IdentityVerification.Domain;

namespace Flit.Modules.IdentityVerification.Ports;

public interface IVerificationEvidenceRepository
{
    Task AddAsync(VerificationEvidence evidence, CancellationToken ct);

    Task<VerificationEvidence?> FindSignatureBySessionIdAsync(
        Guid tenantId,
        Guid verificationSessionId,
        CancellationToken ct);

    Task<IReadOnlyList<VerificationEvidence>> ListExpiredAsync(
        DateTimeOffset capturedBefore,
        CancellationToken ct);

    Task<IReadOnlyList<VerificationEvidence>> ListBySessionIdAsync(
        Guid tenantId,
        Guid verificationSessionId,
        CancellationToken ct);

    Task UpdateAsync(VerificationEvidence evidence, CancellationToken ct);

    Task SaveChangesAsync(CancellationToken ct);
}
