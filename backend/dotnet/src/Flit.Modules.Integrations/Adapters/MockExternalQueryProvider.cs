using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;
using Flit.Modules.Integrations.Application;
using Flit.Modules.Integrations.Ports;

namespace Flit.Modules.Integrations.Adapters;

/// <summary>Mock DEV sin sandbox — switchable por <see cref="IExternalQueryProvider"/> (INT-01 #9431).</summary>
public sealed class MockExternalQueryProvider : IExternalQueryProvider
{
    /// <summary>Conector de prueba: primer intento lanza transitorio (AC2).</summary>
    public const string TransientTestConnector = "MOCK_TRANSIENT_ONCE";

    private readonly ConcurrentDictionary<string, int> _attemptsByKey = new();

    public Task<ExternalQueryProviderResult> ExecuteAsync(
        ExternalQueryProviderRequest request,
        CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        var key = $"{request.TenantId:N}:{request.QueryConnectorCode}";

        if (string.Equals(request.QueryConnectorCode, TransientTestConnector, StringComparison.OrdinalIgnoreCase))
        {
            var attempt = _attemptsByKey.AddOrUpdate(key, 1, (_, n) => n + 1);
            if (attempt == 1)
            {
                throw new TransientExternalQueryException("Mock timeout transitorio (primer intento).");
            }
        }

        var response = MockVerifikResponseFactory.Build(
            request.QueryConnectorCode,
            request.Payload);

        return Task.FromResult(new ExternalQueryProviderResult(
            Succeeded: true,
            HttpStatus: 200,
            Response: response,
            ErrorMessage: null,
            LatencyMs: (int)Math.Max(1, sw.ElapsedMilliseconds)));
    }
}
