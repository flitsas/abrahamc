using Flit.Modules.IdentityVerification.Domain;
using Flit.Modules.IdentityVerification.Ports;
using Flit.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Flit.Infrastructure.Repositories;

public sealed class EfVerificationAiVerdictRepository(FlitDbContext db) : IVerificationAiVerdictRepository
{
    public Task AddAsync(VerificationAiVerdict verdict, CancellationToken ct)
    {
        db.VerificationAiVerdicts.Add(verdict);
        return Task.CompletedTask;
    }

    public Task<VerificationAiVerdict?> FindBySessionIdAsync(
        Guid tenantId,
        Guid verificationSessionId,
        CancellationToken ct) =>
        db.VerificationAiVerdicts.FirstOrDefaultAsync(
            v => v.TenantId == tenantId && v.VerificationSessionId == verificationSessionId,
            ct);

    public Task UpdateAsync(VerificationAiVerdict verdict, CancellationToken ct)
    {
        db.VerificationAiVerdicts.Update(verdict);
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
}
