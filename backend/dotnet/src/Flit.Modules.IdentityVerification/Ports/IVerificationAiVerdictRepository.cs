using Flit.Modules.IdentityVerification.Domain;

namespace Flit.Modules.IdentityVerification.Ports;

public interface IVerificationAiVerdictRepository
{
    Task AddAsync(VerificationAiVerdict verdict, CancellationToken ct);

    Task<VerificationAiVerdict?> FindBySessionIdAsync(
        Guid tenantId,
        Guid verificationSessionId,
        CancellationToken ct);

    Task UpdateAsync(VerificationAiVerdict verdict, CancellationToken ct);

    Task SaveChangesAsync(CancellationToken ct);
}
