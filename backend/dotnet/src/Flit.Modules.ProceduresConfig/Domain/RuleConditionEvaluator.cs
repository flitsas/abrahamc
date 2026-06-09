using System.Globalization;
using System.Text.Json;

namespace Flit.Modules.ProceduresConfig.Domain;

/// <summary>
/// Evalúa árboles JSONB AND/OR con operadores cerrados (ADR-0011).
/// Sin SQL ni expresiones arbitrarias — solo claves de campo del trámite.
/// </summary>
public static class RuleConditionEvaluator
{
    public static bool Evaluate(JsonElement node, IReadOnlyDictionary<string, string?> fields)
    {
        if (node.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return false;
        }

        if (node.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        if (node.TryGetProperty("op", out var opProp))
        {
            var op = opProp.GetString();
            if (op is not ("AND" or "OR"))
            {
                return false;
            }

            if (!node.TryGetProperty("children", out var children) || children.ValueKind != JsonValueKind.Array)
            {
                return false;
            }

            if (children.GetArrayLength() == 0)
            {
                return false;
            }

            return op == "AND"
                ? children.EnumerateArray().All(child => Evaluate(child, fields))
                : children.EnumerateArray().Any(child => Evaluate(child, fields));
        }

        return EvaluateLeaf(node, fields);
    }

    private static bool EvaluateLeaf(JsonElement leaf, IReadOnlyDictionary<string, string?> fields)
    {
        if (!leaf.TryGetProperty("field", out var fieldProp) ||
            !leaf.TryGetProperty("operator", out var operatorProp))
        {
            return false;
        }

        var fieldKey = fieldProp.GetString();
        var op = operatorProp.GetString();
        if (string.IsNullOrWhiteSpace(fieldKey) || string.IsNullOrWhiteSpace(op))
        {
            return false;
        }

        fields.TryGetValue(fieldKey, out var actual);

        return op switch
        {
            "isEmpty" => string.IsNullOrWhiteSpace(actual),
            "isNotEmpty" => !string.IsNullOrWhiteSpace(actual),
            "equal" => CompareResolved(leaf, fields, actual, static (a, b) =>
                string.Equals(a, b, StringComparison.OrdinalIgnoreCase)),
            "notEqual" => !CompareResolved(leaf, fields, actual, static (a, b) =>
                string.Equals(a, b, StringComparison.OrdinalIgnoreCase)),
            "contains" => CompareResolved(leaf, fields, actual, static (a, b) =>
                a.Contains(b, StringComparison.OrdinalIgnoreCase)),
            "greater" => CompareNumeric(leaf, fields, actual, static (a, b) => a > b),
            "less" => CompareNumeric(leaf, fields, actual, static (a, b) => a < b),
            _ => false,
        };
    }

    private static bool CompareResolved(
        JsonElement leaf,
        IReadOnlyDictionary<string, string?> fields,
        string? actual,
        Func<string, string, bool> compare)
    {
        if (!leaf.TryGetProperty("value", out var valueNode) || valueNode.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        var expected = ResolveValue(valueNode, fields);
        if (expected is null || actual is null)
        {
            return false;
        }

        return compare(actual, expected);
    }

    private static bool CompareNumeric(
        JsonElement leaf,
        IReadOnlyDictionary<string, string?> fields,
        string? actual,
        Func<decimal, decimal, bool> compare)
    {
        if (!leaf.TryGetProperty("value", out var valueNode) || valueNode.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        var expected = ResolveValue(valueNode, fields);
        if (expected is null || actual is null)
        {
            return false;
        }

        if (!decimal.TryParse(actual, NumberStyles.Number, CultureInfo.InvariantCulture, out var actualNum) ||
            !decimal.TryParse(expected, NumberStyles.Number, CultureInfo.InvariantCulture, out var expectedNum))
        {
            return false;
        }

        return compare(actualNum, expectedNum);
    }

    private static string? ResolveValue(JsonElement valueNode, IReadOnlyDictionary<string, string?> fields)
    {
        if (!valueNode.TryGetProperty("kind", out var kindProp))
        {
            return null;
        }

        return kindProp.GetString() switch
        {
            "static" => valueNode.TryGetProperty("value", out var staticVal)
                ? staticVal.ValueKind switch
                {
                    JsonValueKind.String => staticVal.GetString(),
                    JsonValueKind.Number => staticVal.GetRawText(),
                    JsonValueKind.True => "true",
                    JsonValueKind.False => "false",
                    _ => staticVal.GetRawText(),
                }
                : null,
            "field" => valueNode.TryGetProperty("field", out var refField)
                ? fields.GetValueOrDefault(refField.GetString() ?? string.Empty)
                : null,
            _ => null,
        };
    }
}
