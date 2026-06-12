namespace Flit.Modules.Companies.Ports;

public sealed record QuipuxWebhookProcessResult(
    bool SignatureValid,
    bool ProcedureUpdated,
    Guid? ProcedureId,
    string? NewState,
    string ResultMessage);

/// <summary>Adaptador extensible mock/real Quipux (HU #9698 AC4).</summary>
public interface IQuipuxWebhookAdapter
{
    bool ValidateSignature(string? signature, string payloadJson);

    QuipuxWebhookProcessResult ParseAndMap(string eventType, string payloadJson);
}
