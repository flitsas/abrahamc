using Flit.Modules.IdentityVerification.Domain;
using Flit.Modules.IdentityVerification.Ports;
using Flit.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Flit.Infrastructure.Repositories;

public sealed class EfVerificationSessionStepRepository(FlitDbContext db) : IVerificationSessionStepRepository
{
    public async Task<IReadOnlyList<VerificationSessionStep>> GetBySessionIdAsync(
        Guid tenantId,
        Guid verificationSessionId,
        CancellationToken ct) =>
        await db.VerificationSessionSteps
            .Where(s => s.TenantId == tenantId && s.VerificationSessionId == verificationSessionId)
            .OrderBy(s => s.StepNumber)
            .ToListAsync(ct);

    public Task AddRangeAsync(IReadOnlyList<VerificationSessionStep> steps, CancellationToken ct)
    {
        db.VerificationSessionSteps.AddRange(steps);
        return Task.CompletedTask;
    }

    public Task UpdateAsync(VerificationSessionStep sessionStep, CancellationToken ct)
    {
        db.VerificationSessionSteps.Update(sessionStep);
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
}
