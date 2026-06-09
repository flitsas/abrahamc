using Flit.Modules.Identity.Ports;

namespace Flit.Modules.Identity.Adapters;

/// <summary>
/// No-op para dev sin python-ml disponible (siempre devuelve fallo
/// con codigo NO_OCR_CONFIGURED). El Register sin imagen sigue
/// funcionando, solo no se valida OCR.
/// </summary>
public sealed class NoOpOcrCedulaClient : IOcrCedulaClient
{
    public Task<OcrCedulaResult> ExtraerAsync(
        string imagenBase64, string mimeType, CancellationToken ct)
        => Task.FromResult(new OcrCedulaResult(
            Success: false,
            Numero: null, Nombres: null, Apellidos: null, ConfidenceGlobal: null,
            ErrorCode: "NO_OCR_CONFIGURED",
            ErrorMessage: "OcrCedulaClient deshabilitado (registrar HttpOcrCedulaClient)"));
}
