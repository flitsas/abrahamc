using Flit.Modules.IdentityVerification.Domain;
using Flit.Modules.IdentityVerification.Ports;

namespace Flit.Modules.IdentityVerification.Adapters;

/// <summary>Repositorio en memoria dedicado a tests TRA-03 (#9435).</summary>
public sealed class InMemoryVerificationSessionRepository : IVerificationSessionRepository
{
    private readonly List<VerificationSession> _store = [];

    public IReadOnlyList<VerificationSession> All => _store.AsReadOnly();

    public Task<VerificationSession?> GetByIdAsync(
        Guid tenantId,
        Guid verificationSessionId,
        CancellationToken ct)
    {
        var hit = _store.FirstOrDefault(s => s.Id == verificationSessionId && s.TenantId == tenantId);
        return Task.FromResult(hit);
    }

    public Task<VerificationSession?> GetByIdForPublicFlowAsync(
        Guid verificationSessionId,
        CancellationToken ct)
    {
        var hit = _store.FirstOrDefault(s => s.Id == verificationSessionId);
        return Task.FromResult(hit);
    }

    public Task<VerificationSession?> FindByInvitationIdAsync(
        Guid tenantId,
        Guid invitationId,
        CancellationToken ct)
    {
        var hit = _store.FirstOrDefault(
            s => s.TenantId == tenantId && s.VerificationInvitationId == invitationId);
        return Task.FromResult(hit);
    }

    public Task<VerificationSession?> FindReusablePassedSessionAsync(
        Guid tenantId,
        string documentTypeCode,
        string documentNumber,
        DateTimeOffset asOf,
        CancellationToken ct = default)
    {
        var hit = _store
            .Where(s =>
                s.TenantId == tenantId
                && string.Equals(s.DocumentTypeCode, documentTypeCode, StringComparison.OrdinalIgnoreCase)
                && string.Equals(s.SubjectDocumentNumber, documentNumber, StringComparison.OrdinalIgnoreCase)
                && string.Equals(s.Status, SessionStatus.Passed, StringComparison.OrdinalIgnoreCase)
                && s.ExpiresAt > asOf)
            .OrderByDescending(s => s.PerformedAt)
            .FirstOrDefault();

        return Task.FromResult(hit);
    }

    public Task AddAsync(VerificationSession session, CancellationToken ct)
    {
        _store.RemoveAll(s => s.Id == session.Id);
        _store.Add(session);
        return Task.CompletedTask;
    }

    public Task UpdateAsync(VerificationSession session, CancellationToken ct) => AddAsync(session, ct);

    public Task SaveAsync(VerificationSession session, CancellationToken ct = default) => AddAsync(session, ct);

    public Task SaveChangesAsync(CancellationToken ct) => Task.CompletedTask;
}
