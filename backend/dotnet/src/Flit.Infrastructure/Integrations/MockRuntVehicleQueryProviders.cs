using Flit.Modules.Companies.Domain;
using Flit.Modules.Companies.Ports;

namespace Flit.Infrastructure.Integrations;

/// <summary>Proveedores mock DEV (#9447). Sin sandbox Verifik/Intempo.</summary>
public sealed class MockRuntVehicleQueryProvider : IRuntVehicleQueryProvider
{
    public string ProviderCode => RuntProviderCode.Runt;

    public Task<RuntVehicleQueryAttemptResult> QueryVehicleAsync(
        RuntVehicleQueryRequest request,
        CancellationToken ct = default)
    {
        if (request.SimulateAllProvidersDown || request.Plate == "FAIL_ALL")
        {
            return Task.FromResult(new RuntVehicleQueryAttemptResult(
                false,
                $$"""{"plate":"{{request.Plate}}","provider":"{{ProviderCode}}"}""",
                "failed",
                "mock_runt_unavailable"));
        }

        if (request.SimulateRuntUnavailable)
        {
            return Task.FromResult(new RuntVehicleQueryAttemptResult(
                false,
                $$"""{"plate":"{{request.Plate}}","provider":"{{ProviderCode}}"}""",
                "timeout",
                "mock_runt_timeout"));
        }

        return Task.FromResult(new RuntVehicleQueryAttemptResult(
            true,
            $$"""{"plate":"{{request.Plate}}","make":"TOYOTA","line":"COROLLA","model_year":2020,"mock_provider":"{{ProviderCode}}"}""",
            "ok",
            null));
    }
}

public sealed class MockVerifikVehicleQueryProvider : IRuntVehicleQueryProvider
{
    public string ProviderCode => RuntProviderCode.Verifik;

    public Task<RuntVehicleQueryAttemptResult> QueryVehicleAsync(
        RuntVehicleQueryRequest request,
        CancellationToken ct = default)
    {
        if (request.SimulateAllProvidersDown || request.Plate == "FAIL_ALL")
        {
            return Task.FromResult(new RuntVehicleQueryAttemptResult(
                false,
                $$"""{"plate":"{{request.Plate}}","provider":"{{ProviderCode}}"}""",
                "failed",
                "mock_verifik_down"));
        }

        return Task.FromResult(new RuntVehicleQueryAttemptResult(
            true,
            $$"""{"plate":"{{request.Plate}}","make":"TOYOTA","line":"COROLLA","model_year":2020,"mock_provider":"verifik-stub"}""",
            "ok",
            null));
    }
}

public sealed class MockIntempoVehicleQueryProvider : IRuntVehicleQueryProvider
{
    public string ProviderCode => RuntProviderCode.Intempo;

    public Task<RuntVehicleQueryAttemptResult> QueryVehicleAsync(
        RuntVehicleQueryRequest request,
        CancellationToken ct = default)
    {
        if (request.SimulateAllProvidersDown || request.Plate == "FAIL_ALL")
        {
            return Task.FromResult(new RuntVehicleQueryAttemptResult(
                false,
                $$"""{"plate":"{{request.Plate}}","provider":"{{ProviderCode}}"}""",
                "failed",
                "mock_intempo_down"));
        }

        return Task.FromResult(new RuntVehicleQueryAttemptResult(
            true,
            $$"""{"plate":"{{request.Plate}}","make":"MAZDA","line":"3","model_year":2019,"mock_provider":"intempo-stub"}""",
            "ok",
            null));
    }
}
