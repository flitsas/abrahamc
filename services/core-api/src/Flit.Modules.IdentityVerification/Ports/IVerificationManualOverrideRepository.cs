using Flit.Modules.IdentityVerification.Domain;

namespace Flit.Modules.IdentityVerification.Ports;

public interface IVerificationManualOverrideRepository
{
    Task AddAsync(VerificationManualOverride manualOverride, CancellationToken ct);

    Task<VerificationManualOverride?> FindLatestBySessionIdAsync(
        Guid tenantId,
        Guid verificationSessionId,
        CancellationToken ct);

    Task SaveChangesAsync(CancellationToken ct);
}
