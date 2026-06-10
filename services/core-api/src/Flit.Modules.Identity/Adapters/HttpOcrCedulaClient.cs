using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Flit.Modules.Identity.Ports;

namespace Flit.Modules.Identity.Adapters;

/// <summary>
/// HttpClient hacia python-ml POST /ocr/cedula:json (MVP Fase 4).
/// Sin Refit por AOT (R1 spike pendiente); usamos HttpClient + JSON
/// source generators.
///
/// Habeas Data Ley 1581: el caller debe auditar antes de invocar.
/// Este cliente NO persiste la imagen ni el resultado.
/// </summary>
public sealed class HttpOcrCedulaClient : IOcrCedulaClient
{
    private readonly HttpClient _http;

    public HttpOcrCedulaClient(HttpClient http)
    {
        _http = http;
        _http.Timeout = TimeSpan.FromSeconds(15);
    }

    public async Task<OcrCedulaResult> ExtraerAsync(
        string imagenBase64, string mimeType, CancellationToken ct)
    {
        try
        {
            var request = new OcrRequest(imagenBase64, mimeType);
            var response = await _http.PostAsJsonAsync(
                "ocr/cedula:json", request, OcrJsonContext.Default.OcrRequest, ct);

            if (response.IsSuccessStatusCode)
            {
                var ok = await response.Content.ReadFromJsonAsync(
                    OcrJsonContext.Default.OcrSuccessResponse, ct);
                if (ok?.Cedula is null)
                    return new OcrCedulaResult(false, null, null, null, null,
                        "OCR_INVALID_RESPONSE", "respuesta sin campo cedula");

                return new OcrCedulaResult(
                    Success: true,
                    Numero: ok.Cedula.Numero,
                    Nombres: ok.Cedula.Nombres,
                    Apellidos: ok.Cedula.Apellidos,
                    ConfidenceGlobal: ok.Cedula.ConfidenceGlobal,
                    ErrorCode: null,
                    ErrorMessage: null);
            }

            // Error: leer detalle si esta disponible.
            var errBody = await response.Content.ReadAsStringAsync(ct);
            string? code = null;
            string? message = null;
            try
            {
                using var doc = JsonDocument.Parse(errBody);
                if (doc.RootElement.TryGetProperty("detail", out var detail))
                {
                    if (detail.ValueKind == JsonValueKind.Object)
                    {
                        code = detail.TryGetProperty("code", out var c) ? c.GetString() : null;
                        message = detail.TryGetProperty("message", out var m) ? m.GetString() : null;
                    }
                }
            }
            catch (JsonException) { /* ignore */ }

            return new OcrCedulaResult(false, null, null, null, null,
                code ?? "OCR_HTTP_ERROR",
                message ?? $"python-ml respondio {(int)response.StatusCode}");
        }
        catch (TaskCanceledException)
        {
            return new OcrCedulaResult(false, null, null, null, null,
                "OCR_TIMEOUT", "python-ml /ocr/cedula:json timeout 15s");
        }
        catch (HttpRequestException ex)
        {
            return new OcrCedulaResult(false, null, null, null, null,
                "OCR_NETWORK", ex.Message);
        }
    }
}

// ─── DTOs internos (JSON source-gen AOT) ─────────────────────
internal sealed record OcrRequest(
    [property: JsonPropertyName("imagen_base64")] string ImagenBase64,
    [property: JsonPropertyName("mime_type")] string MimeType);

internal sealed record CedulaCampos(
    [property: JsonPropertyName("numero")] string Numero,
    [property: JsonPropertyName("nombres")] string Nombres,
    [property: JsonPropertyName("apellidos")] string Apellidos,
    [property: JsonPropertyName("confidence_global")] double ConfidenceGlobal);

internal sealed record OcrSuccessResponse(
    [property: JsonPropertyName("cedula")] CedulaCampos Cedula);

[JsonSerializable(typeof(OcrRequest))]
[JsonSerializable(typeof(OcrSuccessResponse))]
[JsonSerializable(typeof(CedulaCampos))]
internal sealed partial class OcrJsonContext : JsonSerializerContext;
