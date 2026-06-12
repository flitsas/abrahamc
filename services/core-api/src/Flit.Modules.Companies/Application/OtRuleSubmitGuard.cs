using System.Text.Json;
using Flit.Modules.Companies.Domain;
using Flit.Modules.Companies.Ports;
using Flit.Modules.Procedures.Domain;
using Flit.SharedKernel;

namespace Flit.Modules.Companies.Application;

/// <summary>Pre-check OT on_submit antes de transicionar a pendiente (HU #9699).</summary>
public static class OtRuleSubmitGuard
{
    public static async Task<string?> GetBlockMessageAsync(
        ProcedureInstance instance,
        string toState,
        IOtRuleRepository rulesRepo,
        IClock clock,
        CancellationToken ct)
    {
        if (instance.TrafficAgencyId is null ||
            !string.Equals(toState, ProcedureStates.Pendiente, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var fields = ExtractCapturedFields(instance.ConfigSnapshot);
        var result = await EvaluateOtRules.HandleAsync(
            instance.TrafficAgencyId.Value,
            OtRule.TriggerEvents.OnSubmit,
            fields,
            rulesRepo,
            clock,
            ct);

        return result.IsSuccess && result.Value.Count > 0
            ? result.Value[0].Message
            : null;
    }

    private static Dictionary<string, string?> ExtractCapturedFields(string configSnapshotJson)
    {
        var fields = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(configSnapshotJson))
            return fields;

        try
        {
            using var doc = JsonDocument.Parse(configSnapshotJson);
            if (doc.RootElement.TryGetProperty("captured_fields", out var captured) &&
                captured.ValueKind == JsonValueKind.Object)
            {
                foreach (var prop in captured.EnumerateObject())
                    fields[prop.Name] = prop.Value.ToString();
            }
        }
        catch (JsonException)
        {
            // ignore
        }

        return fields;
    }
}
