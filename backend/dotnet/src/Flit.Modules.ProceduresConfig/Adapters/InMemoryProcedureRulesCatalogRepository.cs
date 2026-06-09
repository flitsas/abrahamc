using System.Text.Json;
using Flit.Modules.ProceduresConfig.Domain;
using Flit.Modules.ProceduresConfig.Ports;

namespace Flit.Modules.ProceduresConfig.Adapters;

/// <summary>CRUD de reglas en memoria para DEV sin PostgreSQL (#9440).</summary>
public sealed class InMemoryProcedureRulesCatalogRepository : IProcedureRulesCatalogRepository
{
    private readonly List<ProcedureRuleCatalogRecord> _rules =
    [
        new(
            InMemoryProcedureRulesRepository.DemoGreenDiscountRuleId,
            InMemoryProcedureRulesRepository.DemoTenantId,
            InMemoryProcedureRulesRepository.DemoProcedureTypeId,
            "Tesla OR Electrico -> Descuentos Verdes",
            "Regla demo RGL-02",
            InMemoryProcedureRulesRepository.DemoConditionTree,
            InMemoryProcedureRulesRepository.DemoActions,
            Priority: 10,
            IsActive: true,
            RowVersion: 1,
            CreatedAt: DateTimeOffset.UtcNow,
            UpdatedAt: DateTimeOffset.UtcNow),
        new(
            InMemoryProcedureRulesRepository.DemoPopupRuleId,
            InMemoryProcedureRulesRepository.DemoTenantId,
            InMemoryProcedureRulesRepository.DemoProcedureTypeId,
            "Marca Otro -> Popup aviso",
            "Regla demo RGL-05 AC1",
            InMemoryProcedureRulesRepository.DemoPopupConditionTree,
            InMemoryProcedureRulesRepository.DemoPopupActions,
            Priority: 20,
            IsActive: true,
            RowVersion: 1,
            CreatedAt: DateTimeOffset.UtcNow,
            UpdatedAt: DateTimeOffset.UtcNow),
        new(
            InMemoryProcedureRulesRepository.DemoEndpointRuleId,
            InMemoryProcedureRulesRepository.DemoTenantId,
            InMemoryProcedureRulesRepository.DemoProcedureTypeId,
            "Hibrido -> Echo Health",
            "Regla demo RGL-05 AC2",
            InMemoryProcedureRulesRepository.DemoEndpointConditionTree,
            InMemoryProcedureRulesRepository.DemoEndpointActions,
            Priority: 30,
            IsActive: true,
            RowVersion: 1,
            CreatedAt: DateTimeOffset.UtcNow,
            UpdatedAt: DateTimeOffset.UtcNow),
    ];

    public Task<IReadOnlyList<ProcedureRuleCatalogRecord>> ListByProcedureTypeAsync(
        Guid tenantId,
        Guid procedureTypeId,
        CancellationToken ct = default)
    {
        var items = _rules
            .Where(r => r.TenantId == tenantId && r.ProcedureTypeId == procedureTypeId)
            .OrderBy(r => r.Priority)
            .ThenBy(r => r.Name)
            .ToList();

        return Task.FromResult<IReadOnlyList<ProcedureRuleCatalogRecord>>(items);
    }

    public Task<ProcedureRuleCatalogRecord?> GetByIdAsync(
        Guid tenantId,
        Guid id,
        CancellationToken ct = default)
    {
        var item = _rules.Find(r => r.TenantId == tenantId && r.Id == id);
        return Task.FromResult(item);
    }

    public Task<ProcedureRuleCatalogRecord> AddAsync(
        ProcedureRuleWriteModel model,
        CancellationToken ct = default)
    {
        if (_rules.Exists(r =>
                r.TenantId == model.TenantId &&
                r.ProcedureTypeId == model.ProcedureTypeId &&
                string.Equals(r.Name, model.Name, StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException("duplicate key: uq_rules_tenant_type_name");
        }

        using var condDoc = JsonDocument.Parse(model.ConditionTreeJson);
        using var actDoc = JsonDocument.Parse(model.ActionsJson);

        var created = new ProcedureRuleCatalogRecord(
            Guid.NewGuid(),
            model.TenantId,
            model.ProcedureTypeId,
            model.Name,
            model.Description,
            condDoc.RootElement.Clone(),
            actDoc.RootElement.Clone(),
            model.Priority,
            model.IsActive,
            RowVersion: 1,
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow);

        _rules.Add(created);
        return Task.FromResult(created);
    }

    public Task<ProcedureRuleCatalogRecord?> UpdateAsync(
        ProcedureRuleWriteModel model,
        CancellationToken ct = default)
    {
        if (model.Id is null)
        {
            return Task.FromResult<ProcedureRuleCatalogRecord?>(null);
        }

        var index = _rules.FindIndex(r => r.TenantId == model.TenantId && r.Id == model.Id);
        if (index < 0 || _rules[index].RowVersion != model.ExpectedRowVersion)
        {
            return Task.FromResult<ProcedureRuleCatalogRecord?>(null);
        }

        using var condDoc = JsonDocument.Parse(model.ConditionTreeJson);
        using var actDoc = JsonDocument.Parse(model.ActionsJson);

        var existing = _rules[index];
        if (_rules.Exists(r =>
                r.TenantId == model.TenantId &&
                r.ProcedureTypeId == existing.ProcedureTypeId &&
                r.Id != model.Id &&
                string.Equals(r.Name, model.Name, StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException("duplicate key: uq_rules_tenant_type_name");
        }

        var updated = existing with
        {
            Name = model.Name,
            Description = model.Description,
            ConditionTree = condDoc.RootElement.Clone(),
            Actions = actDoc.RootElement.Clone(),
            Priority = model.Priority,
            IsActive = model.IsActive,
            RowVersion = existing.RowVersion + 1,
            UpdatedAt = DateTimeOffset.UtcNow,
        };

        _rules[index] = updated;
        return Task.FromResult<ProcedureRuleCatalogRecord?>(updated);
    }

    public Task<bool> SoftDeleteAsync(
        Guid tenantId,
        Guid id,
        Guid deletedBy,
        CancellationToken ct = default)
    {
        var removed = _rules.RemoveAll(r => r.TenantId == tenantId && r.Id == id);
        return Task.FromResult(removed > 0);
    }
}
