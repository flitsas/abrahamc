using Flit.Modules.IdentityVerification.Domain;

namespace Flit.Modules.IdentityVerification.Ports;

/// <summary>
/// Persistencia de verification_sessions (IDSecure + TRA-03 reutilizable).
/// </summary>
public interface IVerificationSessionRepository
{
    Task<VerificationSession?> GetByIdAsync(
        Guid tenantId,
        Guid verificationSessionId,
        CancellationToken ct);

    Task<VerificationSession?> GetByIdForPublicFlowAsync(
        Guid verificationSessionId,
        CancellationToken ct);

    Task<VerificationSession?> FindByInvitationIdAsync(
        Guid tenantId,
        Guid invitationId,
        CancellationToken ct);

    /// <summary>TRA-03: sesion passed vigente para mismo documento (reutilizacion).</summary>
    Task<VerificationSession?> FindReusablePassedSessionAsync(
        Guid tenantId,
        string documentTypeCode,
        string documentNumber,
        DateTimeOffset asOf,
        CancellationToken ct = default);

    Task AddAsync(VerificationSession session, CancellationToken ct);

    Task UpdateAsync(VerificationSession session, CancellationToken ct);

    /// <summary>TRA-03 alias: inserta o actualiza segun exista el id.</summary>
    Task SaveAsync(VerificationSession session, CancellationToken ct = default);

    Task SaveChangesAsync(CancellationToken ct);
}
