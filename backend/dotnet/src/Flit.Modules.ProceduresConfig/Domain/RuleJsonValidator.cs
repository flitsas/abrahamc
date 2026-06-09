using System.Text.Json;

namespace Flit.Modules.ProceduresConfig.Domain;

/// <summary>Validación estructural alineada con <c>is_valid_rule_condition/actions</c> (#9410).</summary>
public static class RuleJsonValidator
{
    private static readonly HashSet<string> Operators = new(StringComparer.Ordinal)
    {
        "equal", "notEqual", "greater", "less", "contains", "isEmpty", "isNotEmpty",
    };

    private static readonly HashSet<string> ActionTypes = new(StringComparer.Ordinal)
    {
        "popup_modal", "inject_section", "call_endpoint", "block",
    };

    public static bool TryValidateCondition(JsonElement node, out string? error)
    {
        error = null;
        if (node.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return true;
        }

        if (node.ValueKind != JsonValueKind.Object)
        {
            error = "La condición debe ser un objeto JSON.";
            return false;
        }

        if (node.TryGetProperty("op", out var opEl))
        {
            var op = opEl.GetString();
            if (op is not ("AND" or "OR"))
            {
                error = "El grupo lógico debe usar op AND u OR.";
                return false;
            }

            if (!node.TryGetProperty("children", out var children) || children.ValueKind != JsonValueKind.Array)
            {
                error = "Un grupo lógico requiere children como arreglo.";
                return false;
            }

            foreach (var child in children.EnumerateArray())
            {
                if (!TryValidateCondition(child, out error))
                {
                    return false;
                }
            }

            return true;
        }

        if (!node.TryGetProperty("field", out _) || !node.TryGetProperty("operator", out var operatorEl))
        {
            error = "Cada condición hoja requiere field y operator.";
            return false;
        }

        var operatorName = operatorEl.GetString();
        if (string.IsNullOrWhiteSpace(operatorName) || !Operators.Contains(operatorName))
        {
            error = "operator inválido.";
            return false;
        }

        if (operatorName is "isEmpty" or "isNotEmpty")
        {
            return true;
        }

        if (!node.TryGetProperty("value", out var valueEl) || valueEl.ValueKind != JsonValueKind.Object)
        {
            error = "La condición requiere value con kind static o field.";
            return false;
        }

        var kind = valueEl.TryGetProperty("kind", out var kindEl) ? kindEl.GetString() : null;
        if (kind is not ("static" or "field"))
        {
            error = "value.kind debe ser static o field.";
            return false;
        }

        return true;
    }

    public static bool TryValidateActions(JsonElement actions, out string? error)
    {
        error = null;
        if (actions.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return true;
        }

        if (actions.ValueKind != JsonValueKind.Array)
        {
            error = "actions debe ser un arreglo.";
            return false;
        }

        foreach (var action in actions.EnumerateArray())
        {
            if (action.ValueKind != JsonValueKind.Object ||
                !action.TryGetProperty("type", out var typeEl))
            {
                error = "Cada acción requiere type.";
                return false;
            }

            var type = typeEl.GetString();
            if (string.IsNullOrWhiteSpace(type) || !ActionTypes.Contains(type))
            {
                error = "type de acción inválido.";
                return false;
            }
        }

        return true;
    }
}
