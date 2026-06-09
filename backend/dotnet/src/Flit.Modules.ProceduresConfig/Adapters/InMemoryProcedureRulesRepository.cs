using System.Text.Json;
using Flit.Modules.ProceduresConfig.Domain;
using Flit.Modules.ProceduresConfig.Ports;

namespace Flit.Modules.ProceduresConfig.Adapters;

/// <summary>Reglas de demo para DEV sin PostgreSQL (AC1 Tesla OR Eléctrico).</summary>
public sealed class InMemoryProcedureRulesRepository : IProcedureRulesRepository
{
    public static readonly Guid DemoTenantId = Guid.Parse("00000000-0000-7000-8001-000000000010");
    public static readonly Guid DemoProcedureTypeId = Guid.Parse("00000000-0000-7000-8002-000000000001");
    public static readonly Guid DemoGreenDiscountRuleId = Guid.Parse("00000000-0000-7000-8003-000000000001");
    public static readonly Guid DemoPopupRuleId = Guid.Parse("00000000-0000-7000-8003-000000000002");
    public static readonly Guid DemoEndpointRuleId = Guid.Parse("00000000-0000-7000-8003-000000000003");

    public static readonly JsonElement DemoPopupConditionTree = JsonRuleElements.Parse("""
        {
          "op": "AND",
          "children": [
            { "field": "marca", "operator": "equal", "value": { "kind": "static", "value": "Otro" } }
          ]
        }
        """);

    public static readonly JsonElement DemoPopupActions = JsonRuleElements.Parse("""
        [
          {
            "type": "popup_modal",
            "params": {
              "title": "Marca no preferencial",
              "body": "Seleccionó «Otro». Revise documentación adicional antes de continuar."
            }
          }
        ]
        """);

    public static readonly JsonElement DemoEndpointConditionTree = JsonRuleElements.Parse("""
        {
          "op": "AND",
          "children": [
            { "field": "tipo_energia", "operator": "equal", "value": { "kind": "static", "value": "Hibrido" } }
          ]
        }
        """);

    public static readonly JsonElement DemoEndpointActions = JsonRuleElements.Parse("""
        [
          {
            "type": "call_endpoint",
            "params": { "endpoint_code": "ECHO_HEALTH" }
          }
        ]
        """);

    public static readonly JsonElement DemoConditionTree = JsonRuleElements.Parse("""
        {
          "op": "OR",
          "children": [
            { "field": "marca", "operator": "equal", "value": { "kind": "static", "value": "Tesla" } },
            { "field": "tipo_energia", "operator": "equal", "value": { "kind": "static", "value": "Electrico" } }
          ]
        }
        """);

    public static readonly JsonElement DemoActions = JsonRuleElements.Parse("""
        [
          {
            "type": "inject_section",
            "params": { "section_code": "DESCUENTOS_VERDES", "section_label": "Descuentos Verdes" }
          }
        ]
        """);

    private readonly InMemoryProcedureRulesCatalogRepository _catalog;

    public InMemoryProcedureRulesRepository(InMemoryProcedureRulesCatalogRepository catalog)
    {
        _catalog = catalog;
    }

    public async Task<IReadOnlyList<ProcedureRuleRecord>> ListActiveForEvaluationAsync(
        Guid tenantId,
        Guid procedureTypeId,
        CancellationToken ct = default)
    {
        var items = await _catalog.ListByProcedureTypeAsync(tenantId, procedureTypeId, ct);
        return items
            .Where(r => r.IsActive)
            .OrderBy(r => r.Priority)
            .Select(r => new ProcedureRuleRecord(
                r.Id,
                r.Name,
                r.Priority,
                r.ConditionTree,
                r.Actions))
            .ToList();
    }

    /// <summary>Simula desactivación hot-swap (is_active=false) sin afectar snapshots.</summary>
    public void SetRuleActive(Guid ruleId, bool isActive)
    {
        if (!isActive)
        {
            _ = _catalog.SoftDeleteAsync(DemoTenantId, ruleId, Guid.Empty);
        }
    }
}
