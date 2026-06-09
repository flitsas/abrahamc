namespace Flit.Modules.Companies.Ports;

/// <summary>
/// Adaptador switchable por proveedor (RUNT / Verifik / Intempo). DEV: implementaciones mock (#9447 / INT-01 stub).
/// </summary>
public interface IRuntVehicleQueryProvider
{
    string ProviderCode { get; }

    Task<RuntVehicleQueryAttemptResult> QueryVehicleAsync(
        RuntVehicleQueryRequest request,
        CancellationToken ct = default);
}

public sealed record RuntVehicleQueryRequest(
    Guid TenantId,
    string Plate,
    bool SimulateRuntUnavailable,
    bool SimulateAllProvidersDown);

public sealed record RuntVehicleQueryAttemptResult(
    bool Succeeded,
    string ResultJson,
    string Outcome,
    string? ErrorMessage);
