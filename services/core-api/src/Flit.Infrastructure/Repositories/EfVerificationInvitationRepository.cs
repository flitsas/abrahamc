using Flit.Modules.IdentityVerification.Domain;
using Flit.Modules.IdentityVerification.Ports;
using Flit.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Flit.Infrastructure.Repositories;

public sealed class EfVerificationInvitationRepository(FlitDbContext db, PostgresRlsSession rls)
    : IVerificationInvitationRepository
{
    public async Task<VerificationInvitation?> FindByTokenHashAsync(string tokenHash, CancellationToken ct)
    {
        await rls.EnableSuperAdminReadAsync(ct);
        try
        {
            var invitation = await db.VerificationInvitations
                .FirstOrDefaultAsync(i => i.TokenHash == tokenHash, ct);
            if (invitation is not null)
                await rls.BindTenantFromInvitationLookupAsync(invitation.TenantId, ct);
            return invitation;
        }
        finally
        {
            await rls.DisableSuperAdminReadAsync(ct);
        }
    }

    public Task<VerificationInvitation?> GetByIdAsync(Guid tenantId, Guid invitationId, CancellationToken ct) =>
        db.VerificationInvitations.FirstOrDefaultAsync(
            i => i.TenantId == tenantId && i.Id == invitationId, ct);

    public async Task<VerificationEmailTemplate?> FindEmailTemplateAsync(
        Guid tenantId,
        string templateKey,
        Guid? procedureTypeId,
        CancellationToken ct)
    {
        if (procedureTypeId is { } typeId)
        {
            var specific = await db.VerificationEmailTemplates
                .Where(t => t.IsActive
                            && t.TemplateKey == templateKey
                            && (t.TenantId == null || t.TenantId == tenantId)
                            && t.ProcedureTypeId == typeId)
                .OrderByDescending(t => t.TenantId != null)
                .FirstOrDefaultAsync(ct);

            if (specific is not null)
                return specific;
        }

        return await db.VerificationEmailTemplates
            .Where(t => t.IsActive
                        && t.TemplateKey == templateKey
                        && (t.TenantId == null || t.TenantId == tenantId)
                        && t.ProcedureTypeId == null)
            .OrderByDescending(t => t.TenantId != null)
            .FirstOrDefaultAsync(ct);
    }

    public Task UpdateAsync(VerificationInvitation invitation, CancellationToken ct)
    {
        db.VerificationInvitations.Update(invitation);
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
}
