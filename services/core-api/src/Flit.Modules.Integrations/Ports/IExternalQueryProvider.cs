using System.Text.Json;

namespace Flit.Modules.Integrations.Ports;

/// <summary>Ejecuta la consulta contra el conector externo (Verifik/RUNT mock en DEV — INT-01 #9431).</summary>
public interface IExternalQueryProvider
{
    Task<ExternalQueryProviderResult> ExecuteAsync(ExternalQueryProviderRequest request, CancellationToken ct = default);
}

public sealed record ExternalQueryProviderRequest(
    Guid TenantId,
    string QueryConnectorCode,
    Guid? ProcedureInstanceId,
    string? EdgeRole,
    JsonElement Payload);

public sealed record ExternalQueryProviderResult(
    bool Succeeded,
    int? HttpStatus,
    JsonElement? Response,
    string? ErrorMessage,
    int LatencyMs);
