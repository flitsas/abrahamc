using Flit.Modules.IdentityVerification.Domain;
using Flit.Modules.IdentityVerification.Ports;
using Flit.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Flit.Infrastructure.Repositories;

public sealed class EfVerificationSessionRepository(FlitDbContext db, PostgresRlsSession rls)
    : IVerificationSessionRepository
{
    public Task<VerificationSession?> GetByIdAsync(
        Guid tenantId,
        Guid verificationSessionId,
        CancellationToken ct) =>
        db.VerificationSessions.FirstOrDefaultAsync(
            s => s.TenantId == tenantId && s.Id == verificationSessionId, ct);

    public async Task<VerificationSession?> GetByIdForPublicFlowAsync(
        Guid verificationSessionId,
        CancellationToken ct)
    {
        await rls.EnableSuperAdminReadAsync(ct);
        try
        {
            var session = await db.VerificationSessions
                .FirstOrDefaultAsync(s => s.Id == verificationSessionId, ct);
            if (session is not null)
                await rls.BindTenantFromInvitationLookupAsync(session.TenantId, ct);
            return session;
        }
        finally
        {
            await rls.DisableSuperAdminReadAsync(ct);
        }
    }

    public Task<VerificationSession?> FindByInvitationIdAsync(
        Guid tenantId,
        Guid invitationId,
        CancellationToken ct) =>
        db.VerificationSessions.FirstOrDefaultAsync(
            s => s.TenantId == tenantId && s.VerificationInvitationId == invitationId, ct);

    public Task AddAsync(VerificationSession session, CancellationToken ct)
    {
        db.VerificationSessions.Add(session);
        return Task.CompletedTask;
    }

    public Task UpdateAsync(VerificationSession session, CancellationToken ct)
    {
        db.VerificationSessions.Update(session);
        return Task.CompletedTask;
    }

    public Task<VerificationSession?> FindReusablePassedSessionAsync(
        Guid tenantId,
        string documentTypeCode,
        string documentNumber,
        DateTimeOffset asOf,
        CancellationToken ct = default)
    {
        var typeId = IdentityDocumentTypes.ResolveTypeId(documentTypeCode);
        return db.VerificationSessions
            .Where(s =>
                s.TenantId == tenantId
                && s.SubjectDocumentTypeId == typeId
                && s.SubjectDocumentNumber == documentNumber
                && s.Status == SessionStatus.Passed
                && s.ExpiresAt > asOf)
            .OrderByDescending(s => s.UpdatedAt)
            .FirstOrDefaultAsync(ct);
    }

    public async Task SaveAsync(VerificationSession session, CancellationToken ct = default)
    {
        var exists = await db.VerificationSessions.AnyAsync(s => s.Id == session.Id, ct);
        if (exists)
            db.VerificationSessions.Update(session);
        else
            db.VerificationSessions.Add(session);

        await db.SaveChangesAsync(ct);
    }

    public Task SaveChangesAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
}
