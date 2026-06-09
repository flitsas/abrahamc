using Flit.Modules.ProceduresConfig.Domain;

namespace Flit.Modules.ProceduresConfig.Ports;

public interface IProcedureRulesCatalogRepository
{
    Task<IReadOnlyList<ProcedureRuleCatalogRecord>> ListByProcedureTypeAsync(
        Guid tenantId,
        Guid procedureTypeId,
        CancellationToken ct = default);

    Task<ProcedureRuleCatalogRecord?> GetByIdAsync(
        Guid tenantId,
        Guid id,
        CancellationToken ct = default);

    Task<ProcedureRuleCatalogRecord> AddAsync(ProcedureRuleWriteModel model, CancellationToken ct = default);

    Task<ProcedureRuleCatalogRecord?> UpdateAsync(ProcedureRuleWriteModel model, CancellationToken ct = default);

    Task<bool> SoftDeleteAsync(Guid tenantId, Guid id, Guid deletedBy, CancellationToken ct = default);
}

public sealed record ProcedureRuleWriteModel(
    Guid? Id,
    Guid TenantId,
    Guid ProcedureTypeId,
    string Name,
    string? Description,
    string ConditionTreeJson,
    string ActionsJson,
    int Priority,
    bool IsActive,
    Guid ActorUserId,
    int? ExpectedRowVersion = null);
