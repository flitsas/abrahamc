using System.Text.Json;
using Flit.Modules.Companies.Ports;

namespace Flit.Infrastructure.Adapters;

/// <summary>Mock Quipux — firma dev y mapeo tramite_estado_cambiado (HU #9698).</summary>
public sealed class QuipuxMockWebhookAdapter : IQuipuxWebhookAdapter
{
    public const string MockSignature = "flit-mock-signature-dev";

    public bool ValidateSignature(string? signature, string payloadJson)
    {
        _ = payloadJson;
        return string.Equals(signature, MockSignature, StringComparison.Ordinal);
    }

    public QuipuxWebhookProcessResult ParseAndMap(string eventType, string payloadJson)
    {
        try
        {
            using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(payloadJson) ? "{}" : payloadJson);
            var root = doc.RootElement;

            Guid? procedureId = null;
            if (root.TryGetProperty("tramite_id", out var tramiteId) &&
                Guid.TryParse(tramiteId.GetString(), out var parsedTramite))
            {
                procedureId = parsedTramite;
            }
            else if (root.TryGetProperty("procedure_id", out var procId) &&
                     Guid.TryParse(procId.GetString(), out var parsedProc))
            {
                procedureId = parsedProc;
            }

            var newState = root.TryGetProperty("state", out var stateEl)
                ? stateEl.GetString()
                : eventType.Contains("approved", StringComparison.OrdinalIgnoreCase) ||
                  eventType.Contains("aprobado", StringComparison.OrdinalIgnoreCase)
                    ? "aprobado"
                    : null;

            return new QuipuxWebhookProcessResult(
                SignatureValid: true,
                ProcedureUpdated: procedureId is not null && newState is not null,
                ProcedureId: procedureId,
                NewState: newState,
                ResultMessage: "processed");
        }
        catch (JsonException)
        {
            return new QuipuxWebhookProcessResult(
                false, false, null, null, "invalid_payload");
        }
    }
}
