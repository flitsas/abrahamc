using System.Text.Json;
using Flit.SharedKernel;

namespace Flit.Modules.Companies.Domain;

/// <summary>
/// Regla OT parametrizable con árbol de condición JSONB y lista de acciones JSONB.
/// Espejo de ot.ot_rules (HU #9456 OT-03).
/// La validación estructural del JSONB refleja los CHECKs de BD:
///   is_valid_rule_condition(condition_tree) y is_valid_rule_actions(actions).
/// </summary>
public sealed class OtRule
{
    public static class TriggerEvents
    {
        public const string OnSubmit = "on_submit";
        public const string OnFieldChange = "on_field_change";
        public const string OnApprove = "on_approve";
        public const string OnReject = "on_reject";

        public static readonly IReadOnlySet<string> All =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            { OnSubmit, OnFieldChange, OnApprove, OnReject };
    }

    public static class ActionTypes
    {
        public const string PopupModal = "popup_modal";
        public const string InjectSection = "inject_section";
        public const string CallEndpoint = "call_endpoint";
        public const string Block = "block";

        public static readonly IReadOnlySet<string> All =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            { PopupModal, InjectSection, CallEndpoint, Block };
    }

    public Guid Id { get; private set; }
    public Guid TrafficAgencyId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string TriggerEvent { get; private set; } = TriggerEvents.OnSubmit;
    public string ConditionTreeJson { get; private set; } = "{}";
    public string ActionsJson { get; private set; } = "[]";
    public int Priority { get; private set; } = 100;
    public bool IsActive { get; private set; } = true;
    public DateTimeOffset? ValidFrom { get; private set; }
    public DateTimeOffset? ValidUntil { get; private set; }
    public int SchemaVersion { get; private set; } = 1;
    public DateTimeOffset CreatedAt { get; private set; }
    public Guid CreatedBy { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public Guid UpdatedBy { get; private set; }
    public DateTimeOffset? DeletedAt { get; private set; }
    public Guid? DeletedBy { get; private set; }
    public int RowVersion { get; private set; }

    private OtRule() { }

    public bool IsDeleted => DeletedAt.HasValue;

    /// <summary>
    /// Crea una regla OT validando el nombre, gatillo y la estructura JSONB.
    /// AC2: condición malformada → devuelve error tipado (no lanza excepción).
    /// </summary>
    public static Result<OtRule, string> Create(
        Guid trafficAgencyId,
        string name,
        string triggerEvent,
        string conditionTreeJson,
        string actionsJson,
        int priority,
        DateTimeOffset? validFrom,
        DateTimeOffset? validUntil,
        Guid createdBy,
        DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(name))
            return Result<OtRule, string>.Failure("El nombre de la regla es requerido.");

        if (!TriggerEvents.All.Contains(triggerEvent))
            return Result<OtRule, string>.Failure(
                $"Gatillo '{triggerEvent}' inválido. Valores aceptados: {string.Join(", ", TriggerEvents.All)}.");

        // Validar condition_tree (espejo de is_valid_rule_condition)
        var condError = ValidateConditionTree(conditionTreeJson);
        if (condError is not null)
            return Result<OtRule, string>.Failure($"condition_tree inválido: {condError}");

        // Validar actions (espejo de is_valid_rule_actions)
        var actError = ValidateActions(actionsJson);
        if (actError is not null)
            return Result<OtRule, string>.Failure($"actions inválido: {actError}");

        var rule = new OtRule
        {
            Id = Guid.NewGuid(),
            TrafficAgencyId = trafficAgencyId,
            Name = name.Trim(),
            TriggerEvent = triggerEvent,
            ConditionTreeJson = conditionTreeJson,
            ActionsJson = actionsJson,
            Priority = priority,
            IsActive = true,
            ValidFrom = validFrom,
            ValidUntil = validUntil,
            SchemaVersion = 1,
            CreatedAt = now,
            CreatedBy = createdBy,
            UpdatedAt = now,
            UpdatedBy = createdBy,
            RowVersion = 1,
        };

        return Result<OtRule, string>.Success(rule);
    }

    /// <summary>
    /// Activa o desactiva la regla en hot-swap (AC1).
    /// El cambio es inmediato; la evaluación se aplica sin redeploy.
    /// </summary>
    public void Toggle(Guid updatedBy, DateTimeOffset now)
    {
        IsActive = !IsActive;
        UpdatedBy = updatedBy;
        UpdatedAt = now;
        RowVersion++;
    }

    /// <summary>
    /// Espejo de is_valid_rule_condition(node jsonb) definida en la BD.
    /// Retorna null si válido, o un mensaje de error si inválido.
    /// </summary>
    public static string? ValidateConditionTree(string json)
    {
        if (string.IsNullOrWhiteSpace(json) || json is "null")
            return null;

        try
        {
            using var doc = JsonDocument.Parse(json);
            return ValidateConditionNode(doc.RootElement);
        }
        catch (JsonException ex)
        {
            return $"JSON malformado: {ex.Message}";
        }
    }

    private static string? ValidateConditionNode(JsonElement node)
    {
        if (node.ValueKind == JsonValueKind.Null)
            return null;
        if (node.ValueKind != JsonValueKind.Object)
            return "El nodo debe ser un objeto JSON.";

        // Grupo lógico AND/OR
        if (node.TryGetProperty("op", out var opProp))
        {
            var op = opProp.GetString();
            if (op is not ("AND" or "OR"))
                return $"Operador lógico '{op}' inválido. Solo AND/OR.";
            if (!node.TryGetProperty("children", out var children) ||
                children.ValueKind != JsonValueKind.Array)
                return "Grupo AND/OR debe tener 'children' como array.";
            foreach (var child in children.EnumerateArray())
            {
                var err = ValidateConditionNode(child);
                if (err is not null) return err;
            }
            return null;
        }

        // Hoja: field + operator [+ value]
        if (!node.TryGetProperty("field", out _) || !node.TryGetProperty("operator", out var operProp))
            return "Nodo hoja debe tener 'field' y 'operator'.";

        var oper = operProp.GetString();
        var validOps = new[] { "equal", "notEqual", "greater", "less", "contains", "isEmpty", "isNotEmpty" };
        if (!validOps.Contains(oper, StringComparer.OrdinalIgnoreCase))
            return $"Operador '{oper}' inválido.";

        if (oper is not ("isEmpty" or "isNotEmpty"))
        {
            if (!node.TryGetProperty("value", out var valueProp) ||
                valueProp.ValueKind != JsonValueKind.Object)
                return "Nodo hoja debe tener 'value' como objeto para este operador.";
            if (!valueProp.TryGetProperty("kind", out var kindProp) ||
                kindProp.GetString() is not ("static" or "field"))
                return "value.kind debe ser 'static' o 'field'.";
        }

        return null;
    }

    /// <summary>
    /// Espejo de is_valid_rule_actions(actions jsonb) definida en la BD.
    /// </summary>
    public static string? ValidateActions(string json)
    {
        if (string.IsNullOrWhiteSpace(json) || json is "null")
            return null;

        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (root.ValueKind == JsonValueKind.Null)
                return null;
            if (root.ValueKind != JsonValueKind.Array)
                return "actions debe ser un array JSON.";

            foreach (var action in root.EnumerateArray())
            {
                if (action.ValueKind != JsonValueKind.Object)
                    return "Cada acción debe ser un objeto JSON.";
                if (!action.TryGetProperty("type", out var typeProp))
                    return "Cada acción debe tener 'type'.";
                var type = typeProp.GetString();
                if (!ActionTypes.All.Contains(type ?? string.Empty))
                    return $"Tipo de acción '{type}' inválido. Valores: {string.Join(", ", ActionTypes.All)}.";
            }
            return null;
        }
        catch (JsonException ex)
        {
            return $"JSON malformado: {ex.Message}";
        }
    }
}
