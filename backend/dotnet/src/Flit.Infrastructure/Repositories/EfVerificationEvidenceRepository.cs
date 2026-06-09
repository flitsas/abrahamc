using Flit.Modules.IdentityVerification.Domain;
using Flit.Modules.IdentityVerification.Ports;
using Flit.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Flit.Infrastructure.Repositories;

public sealed class EfVerificationEvidenceRepository(FlitDbContext db) : IVerificationEvidenceRepository
{
    public Task AddAsync(VerificationEvidence evidence, CancellationToken ct)
    {
        db.VerificationEvidences.Add(evidence);
        return Task.CompletedTask;
    }

    public Task<VerificationEvidence?> FindSignatureBySessionIdAsync(
        Guid tenantId,
        Guid verificationSessionId,
        CancellationToken ct) =>
        db.VerificationEvidences.FirstOrDefaultAsync(
            e => e.TenantId == tenantId
                 && e.VerificationSessionId == verificationSessionId
                 && e.EvidenceType == EvidenceTypes.SignatureCanvas,
            ct);

    public async Task<IReadOnlyList<VerificationEvidence>> ListExpiredAsync(
        DateTimeOffset capturedBefore,
        CancellationToken ct) =>
        await db.VerificationEvidences
            .Where(e => e.EvidenceType != EvidenceTypes.Purged && e.CapturedAt < capturedBefore)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<VerificationEvidence>> ListBySessionIdAsync(
        Guid tenantId,
        Guid verificationSessionId,
        CancellationToken ct) =>
        await db.VerificationEvidences
            .Where(e => e.TenantId == tenantId && e.VerificationSessionId == verificationSessionId)
            .ToListAsync(ct);

    public Task UpdateAsync(VerificationEvidence evidence, CancellationToken ct)
    {
        db.VerificationEvidences.Update(evidence);
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
}
