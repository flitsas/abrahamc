using Flit.Infrastructure.Persistence;
using Flit.Modules.IdentityVerification.Domain;
using Flit.Modules.IdentityVerification.Ports;
using Microsoft.EntityFrameworkCore;

namespace Flit.Infrastructure.Repositories;

public sealed class EfVerificationManualOverrideRepository(FlitDbContext db)
    : IVerificationManualOverrideRepository
{
    public Task AddAsync(VerificationManualOverride manualOverride, CancellationToken ct)
    {
        db.VerificationManualOverrides.Add(manualOverride);
        return Task.CompletedTask;
    }

    public Task<VerificationManualOverride?> FindLatestBySessionIdAsync(
        Guid tenantId,
        Guid verificationSessionId,
        CancellationToken ct) =>
        db.VerificationManualOverrides
            .Where(o => o.TenantId == tenantId && o.VerificationSessionId == verificationSessionId)
            .OrderByDescending(o => o.OverriddenAt)
            .FirstOrDefaultAsync(ct);

    public Task SaveChangesAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
}
