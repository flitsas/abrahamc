using Flit.Modules.IdentityVerification.Domain;

namespace Flit.Modules.IdentityVerification.Ports;

public interface IVerificationOcrResultRepository
{
    Task AddAsync(VerificationOcrResult result, CancellationToken ct);

    Task<VerificationOcrResult?> FindBySessionIdAsync(
        Guid tenantId,
        Guid verificationSessionId,
        CancellationToken ct);

    Task UpdateAsync(VerificationOcrResult result, CancellationToken ct);

    Task SaveChangesAsync(CancellationToken ct);
}
