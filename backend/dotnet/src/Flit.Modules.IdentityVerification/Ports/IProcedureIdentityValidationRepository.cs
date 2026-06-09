using Flit.Modules.IdentityVerification.Domain;

namespace Flit.Modules.IdentityVerification.Ports;

public interface IProcedureIdentityValidationRepository
{
    Task AddAsync(ProcedureIdentityValidation validation, CancellationToken ct);

    Task UpdateAsync(ProcedureIdentityValidation validation, CancellationToken ct);

    Task<ProcedureIdentityValidation?> FindBySessionIdAsync(
        Guid tenantId,
        Guid verificationSessionId,
        CancellationToken ct);

    Task<IReadOnlyList<ProcedureIdentityValidation>> GetByProcedureInstanceIdAsync(
        Guid tenantId,
        Guid procedureInstanceId,
        CancellationToken ct);

    /// <summary>TRA-03: enlace por instancia y actor del wizard.</summary>
    Task<ProcedureIdentityValidation?> FindByInstanceAndActorAsync(
        Guid tenantId,
        Guid procedureInstanceId,
        Guid? actorId,
        CancellationToken ct = default);

    Task SaveAsync(ProcedureIdentityValidation link, CancellationToken ct = default);

    Task SaveChangesAsync(CancellationToken ct);
}
