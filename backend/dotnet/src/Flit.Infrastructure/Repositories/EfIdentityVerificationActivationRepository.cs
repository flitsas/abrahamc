using Flit.Modules.IdentityVerification.Application;
using Flit.Modules.IdentityVerification.Domain;
using Flit.Modules.IdentityVerification.Ports;
using Flit.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Flit.Infrastructure.Repositories;

public sealed class EfIdentityVerificationActivationRepository(FlitDbContext db)
    : IIdentityVerificationActivationRepository
{
    public Task<bool> DomainEventExistsAsync(
        Guid tenantId,
        string idempotencyKey,
        CancellationToken ct) =>
        db.VerificationDomainEvents
            .AnyAsync(e => e.TenantId == tenantId && e.IdempotencyKey == idempotencyKey, ct);

    public async Task<IReadOnlyList<ProcedureTypeVerificationConfig>> GetActiveConfigsAsync(
        Guid tenantId,
        Guid procedureTypeId,
        CancellationToken ct) =>
        await db.ProcedureTypeVerificationConfigs
            .Where(c => c.TenantId == tenantId
                        && c.ProcedureTypeId == procedureTypeId
                        && c.IsActive)
            .OrderBy(c => c.SortOrder)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<VerificationInvitation>> GetInvitationsByInstanceAsync(
        Guid tenantId,
        Guid procedureInstanceId,
        CancellationToken ct) =>
        await db.VerificationInvitations
            .Where(i => i.TenantId == tenantId && i.ProcedureInstanceId == procedureInstanceId)
            .ToListAsync(ct);

    public Task RegisterDomainEventAsync(VerificationDomainEvent domainEvent, CancellationToken ct)
    {
        db.VerificationDomainEvents.Add(domainEvent);
        return Task.CompletedTask;
    }

    public Task AddInvitationsAsync(IReadOnlyList<VerificationInvitation> invitations, CancellationToken ct)
    {
        db.VerificationInvitations.AddRange(invitations);
        return Task.CompletedTask;
    }

    public async Task SaveChangesAsync(CancellationToken ct)
    {
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (IsDuplicateIdempotency(ex))
        {
            throw new DuplicateDomainEventException();
        }
    }

    private static bool IsDuplicateIdempotency(DbUpdateException ex)
    {
        var message = ex.InnerException?.Message ?? ex.Message;
        return message.Contains("uq_verification_domain_events_idempotency", StringComparison.OrdinalIgnoreCase)
               || message.Contains("duplicate key", StringComparison.OrdinalIgnoreCase);
    }
}
