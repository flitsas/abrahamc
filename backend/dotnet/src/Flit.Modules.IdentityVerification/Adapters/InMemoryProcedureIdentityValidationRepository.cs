using System.Collections.Concurrent;
using Flit.Modules.IdentityVerification.Domain;
using Flit.Modules.IdentityVerification.Ports;

namespace Flit.Modules.IdentityVerification.Adapters;

/// <summary>
/// Persistencia en memoria de procedure_identity_validations (IDSecure + TRA-03).
/// </summary>
public sealed class InMemoryProcedureIdentityValidationRepository : IProcedureIdentityValidationRepository
{
    private readonly ConcurrentDictionary<Guid, ProcedureIdentityValidation> _validations = new();

    public IReadOnlyList<ProcedureIdentityValidation> All =>
        _validations.Values.ToList().AsReadOnly();

    public Task AddAsync(ProcedureIdentityValidation validation, CancellationToken ct)
    {
        _validations[validation.Id] = validation;
        return Task.CompletedTask;
    }

    public Task UpdateAsync(ProcedureIdentityValidation validation, CancellationToken ct)
    {
        _validations[validation.Id] = validation;
        return Task.CompletedTask;
    }

    public Task<ProcedureIdentityValidation?> FindBySessionIdAsync(
        Guid tenantId,
        Guid verificationSessionId,
        CancellationToken ct)
    {
        var found = _validations.Values.FirstOrDefault(
            v => v.TenantId == tenantId && v.VerificationSessionId == verificationSessionId);
        return Task.FromResult(found);
    }

    public Task<IReadOnlyList<ProcedureIdentityValidation>> GetByProcedureInstanceIdAsync(
        Guid tenantId,
        Guid procedureInstanceId,
        CancellationToken ct)
    {
        var list = _validations.Values
            .Where(v => v.TenantId == tenantId && v.ProcedureInstanceId == procedureInstanceId)
            .ToList();
        return Task.FromResult<IReadOnlyList<ProcedureIdentityValidation>>(list);
    }

    public Task<ProcedureIdentityValidation?> FindByInstanceAndActorAsync(
        Guid tenantId,
        Guid procedureInstanceId,
        Guid? actorId,
        CancellationToken ct = default)
    {
        var hit = _validations.Values.FirstOrDefault(l =>
            l.TenantId == tenantId
            && l.ProcedureInstanceId == procedureInstanceId
            && l.ActorId == actorId);
        return Task.FromResult(hit);
    }

    public Task SaveAsync(ProcedureIdentityValidation link, CancellationToken ct = default)
    {
        _validations[link.Id] = link;
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync(CancellationToken ct) => Task.CompletedTask;
}
