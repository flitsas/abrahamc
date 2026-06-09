using Flit.Modules.IdentityVerification.Domain;

namespace Flit.Modules.IdentityVerification.Ports;

public interface IVerificationSessionStepRepository
{
    Task<IReadOnlyList<VerificationSessionStep>> GetBySessionIdAsync(
        Guid tenantId,
        Guid verificationSessionId,
        CancellationToken ct);

    Task AddRangeAsync(IReadOnlyList<VerificationSessionStep> steps, CancellationToken ct);

    Task UpdateAsync(VerificationSessionStep sessionStep, CancellationToken ct);

    Task SaveChangesAsync(CancellationToken ct);
}
