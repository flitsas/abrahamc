using Flit.Modules.IdentityVerification.Domain;
using Flit.Modules.IdentityVerification.Ports;
using Flit.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Flit.Infrastructure.Repositories;

public sealed class EfVerificationOcrResultRepository(FlitDbContext db) : IVerificationOcrResultRepository
{
    public Task AddAsync(VerificationOcrResult result, CancellationToken ct)
    {
        db.VerificationOcrResults.Add(result);
        return Task.CompletedTask;
    }

    public Task<VerificationOcrResult?> FindBySessionIdAsync(
        Guid tenantId,
        Guid verificationSessionId,
        CancellationToken ct) =>
        db.VerificationOcrResults.FirstOrDefaultAsync(
            r => r.TenantId == tenantId && r.VerificationSessionId == verificationSessionId,
            ct);

    public Task UpdateAsync(VerificationOcrResult result, CancellationToken ct)
    {
        db.VerificationOcrResults.Update(result);
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
}
