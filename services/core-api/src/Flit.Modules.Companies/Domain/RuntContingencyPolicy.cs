using System.Text.Json;

namespace Flit.Modules.Companies.Domain;

/// <summary>Política parseada de company_module_configs (module_key runt_contingency).</summary>
public sealed class RuntContingencyPolicy
{
    public string Primary { get; init; } = RuntProviderCode.Verifik;
    public IReadOnlyList<string> Failover { get; init; } = [];
    public bool MockDev { get; init; } = true;

    public static RuntContingencyPolicy Default => new();

    public static RuntContingencyPolicy Parse(string? configJson)
    {
        if (string.IsNullOrWhiteSpace(configJson))
            return Default;

        try
        {
            using var doc = JsonDocument.Parse(configJson);
            var root = doc.RootElement;
            var primary = root.TryGetProperty("primary", out var p)
                ? p.GetString() ?? RuntProviderCode.Verifik
                : RuntProviderCode.Verifik;
            if (!RuntProviderCode.All.Contains(primary))
                primary = RuntProviderCode.Verifik;

            var failover = new List<string>();
            if (root.TryGetProperty("failover", out var arr) && arr.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in arr.EnumerateArray())
                {
                    var code = item.GetString();
                    if (code is not null && RuntProviderCode.All.Contains(code) && code != primary)
                        failover.Add(code);
                }
            }

            var mockDev = !root.TryGetProperty("mock_dev", out var m) || m.ValueKind != JsonValueKind.False;

            return new RuntContingencyPolicy
            {
                Primary = primary,
                Failover = failover,
                MockDev = mockDev,
            };
        }
        catch (JsonException)
        {
            return Default;
        }
    }

    /// <summary>Orden de intento: RUNT primero, luego primary y failover del tenant (sin duplicados).</summary>
    public IReadOnlyList<string> BuildAttemptChain()
    {
        var chain = new List<string> { RuntProviderCode.Runt };
        if (Primary != RuntProviderCode.Runt)
            chain.Add(Primary);
        foreach (var f in Failover)
        {
            if (!chain.Contains(f, StringComparer.Ordinal))
                chain.Add(f);
        }

        return chain;
    }
}
