using Flit.Infrastructure.Persistence;
using Flit.Modules.IdentityVerification.Domain;
using Flit.Modules.IdentityVerification.Ports;
using Microsoft.EntityFrameworkCore;

namespace Flit.Infrastructure.Repositories;

public sealed class EfIdSecureReviewReadRepository(FlitDbContext db) : IIdSecureReviewReadRepository
{
    public async Task<IReadOnlyList<VerificationSession>> ListSessionsByTenantAsync(
        Guid tenantId,
        CancellationToken ct) =>
        await db.VerificationSessions
            .Where(s => s.TenantId == tenantId)
            .ToListAsync(ct);

    public Task<VerificationInvitation?> FindInvitationByIdAsync(
        Guid tenantId,
        Guid invitationId,
        CancellationToken ct) =>
        db.VerificationInvitations.FirstOrDefaultAsync(
            i => i.TenantId == tenantId && i.Id == invitationId,
            ct);

    public Task<VerificationDomainEvent?> FindDomainEventByInstanceAsync(
        Guid tenantId,
        Guid procedureInstanceId,
        CancellationToken ct) =>
        db.VerificationDomainEvents.FirstOrDefaultAsync(
            e => e.TenantId == tenantId && e.ProcedureInstanceId == procedureInstanceId,
            ct);
}
