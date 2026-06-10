using System.Diagnostics;
using System.Globalization;
using System.Net.Http.Headers;
using System.Text.Json;
using Flit.Modules.Integrations.Application;
using Flit.Modules.Integrations.Ports;

namespace Flit.Modules.Integrations.Adapters;

/// <summary>Consultas reales Verifik.co — referencia: APIs de Consulta del RUNT - Verifik.md (#9467).</summary>
public sealed class VerifikHttpExternalQueryProvider(
    IHttpClientFactory httpClientFactory,
    VerifikOptions options) : IExternalQueryProvider
{
    public async Task<ExternalQueryProviderResult> ExecuteAsync(
        ExternalQueryProviderRequest request,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(options.ApiToken))
        {
            return new ExternalQueryProviderResult(
                false,
                null,
                null,
                "Verifik:ApiToken no configurado.",
                0);
        }

        var build = VerifikQueryUrlBuilder.Build(
            options.BaseUrl,
            request.QueryConnectorCode,
            request.EdgeRole,
            request.Payload);

        if (build.Error is not null)
        {
            return new ExternalQueryProviderResult(false, null, null, build.Error, 0);
        }

        var sw = Stopwatch.StartNew();
        var client = httpClientFactory.CreateClient(nameof(VerifikHttpExternalQueryProvider));
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(options.AuthScheme, options.ApiToken);

        using var httpRequest = new HttpRequestMessage(HttpMethod.Get, build.Url);
        HttpResponseMessage response;
        try
        {
            response = await client.SendAsync(httpRequest, ct);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            throw new TransientExternalQueryException(ex.Message);
        }

        var body = await response.Content.ReadAsStringAsync(ct);
        sw.Stop();

        if (!response.IsSuccessStatusCode)
        {
            return new ExternalQueryProviderResult(
                false,
                (int)response.StatusCode,
                TryParseJson(body),
                $"Verifik HTTP {(int)response.StatusCode}",
                (int)sw.ElapsedMilliseconds);
        }

        return new ExternalQueryProviderResult(
            true,
            (int)response.StatusCode,
            TryParseJson(body),
            null,
            (int)sw.ElapsedMilliseconds);
    }

    private static JsonElement? TryParseJson(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<JsonElement>(body);
        }
        catch
        {
            return JsonSerializer.SerializeToElement(new { raw = body });
        }
    }
}

public sealed class VerifikOptions
{
    public const string SectionName = "Verifik";

    public string BaseUrl { get; init; } = "https://api.verifik.co";

    public string ApiToken { get; init; } = string.Empty;

    public string AuthScheme { get; init; } = "Bearer";

    public int TimeoutSeconds { get; init; } = 30;
}

/// <summary>Construye URLs Verifik v2 según conector y arista.</summary>
public static class VerifikQueryUrlBuilder
{
    public sealed record BuildResult(string? Url, string? Error);

    public static BuildResult Build(
        string baseUrl,
        string connectorCode,
        string? edgeRole,
        JsonElement payload)
    {
        var connector = connectorCode.Trim().ToUpperInvariant();
        var edge = edgeRole?.Trim().ToLowerInvariant() ?? string.Empty;
        var docType = Read(payload, "documentType", "documentTypeCode", "tipo_documento") ?? "CC";
        var docNumber = Read(payload, "documentNumber", "doc_propietario", "doc_comprador", "doc_locatario", "numero_documento", "nit")
            ?? ReadByPrefix(payload, "doc_");
        var plate = Read(payload, "plate", "placa", "noPlaca");
        var rnmcDate = Read(payload, "date", "fecha_rnmc")
            ?? DateTime.UtcNow.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);

        if (string.IsNullOrWhiteSpace(docNumber) && connector is not "FASECOLDA")
        {
            return new BuildResult(null, "documentNumber es obligatorio para la consulta Verifik.");
        }

        var root = baseUrl.TrimEnd('/');
        string path;

        switch (connector)
        {
            case "RUNT" when edge is "vehiculo" or "vehicle":
                if (string.IsNullOrWhiteSpace(plate))
                {
                    return new BuildResult(null, "placa es obligatoria para RUNT vehículo (vehicle-by-plate).");
                }

                path = $"/v2/co/runt/vehicle-by-plate?documentType={Uri.EscapeDataString(docType)}&documentNumber={Uri.EscapeDataString(docNumber!)}&plate={Uri.EscapeDataString(plate)}";
                break;

            case "RUNT":
                path = $"/v2/co/runt/conductor?documentType={Uri.EscapeDataString(docType)}&documentNumber={Uri.EscapeDataString(docNumber!)}";
                break;

            case "SIMIT":
                path = $"/v2/co/simit/resoluciones?documentType={Uri.EscapeDataString(docType)}&documentNumber={Uri.EscapeDataString(docNumber!)}";
                break;

            case "RUES":
                path = $"/v2/co/rues?documentType={Uri.EscapeDataString(docType)}&documentNumber={Uri.EscapeDataString(docNumber!)}";
                break;

            case "RNMC":
                path = $"/v2/co/policia/rnmc?documentType={Uri.EscapeDataString(docType)}&documentNumber={Uri.EscapeDataString(docNumber!)}&date={Uri.EscapeDataString(rnmcDate)}";
                break;

            case "RES":
            case "RESOLUCIONES":
                path = $"/v2/co/simit/resoluciones?documentType={Uri.EscapeDataString(docType)}&documentNumber={Uri.EscapeDataString(docNumber!)}";
                break;

            default:
                return new BuildResult(null, $"Conector '{connectorCode}' no tiene mapeo Verifik HTTP.");
        }

        return new BuildResult(root + path, null);
    }

    private static string? Read(JsonElement payload, params string[] keys)
    {
        if (payload.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        foreach (var key in keys)
        {
            if (payload.TryGetProperty(key, out var prop) && prop.ValueKind == JsonValueKind.String)
            {
                var v = prop.GetString();
                if (!string.IsNullOrWhiteSpace(v))
                {
                    return v.Trim();
                }
            }
        }

        return null;
    }

    private static string? ReadByPrefix(JsonElement payload, string prefix)
    {
        if (payload.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        foreach (var prop in payload.EnumerateObject())
        {
            if (prop.Name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
                && prop.Value.ValueKind == JsonValueKind.String)
            {
                var v = prop.Value.GetString();
                if (!string.IsNullOrWhiteSpace(v))
                {
                    return v.Trim();
                }
            }
        }

        return null;
    }
}
