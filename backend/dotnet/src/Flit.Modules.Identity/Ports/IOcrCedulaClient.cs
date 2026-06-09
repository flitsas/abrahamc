namespace Flit.Modules.Identity.Ports;

/// <summary>
/// Cliente para llamar al endpoint python-ml POST /ocr/cedula:json.
/// MVP Fase 4. Opcional: si Register recibe cedulaImageBase64, llamamos.
/// </summary>
public interface IOcrCedulaClient
{
    Task<OcrCedulaResult> ExtraerAsync(
        string imagenBase64, string mimeType, CancellationToken ct);
}

/// <summary>Resultado del OCR. NO se persiste — solo se usa para validar contra el numero documento de Register.</summary>
public sealed record OcrCedulaResult(
    bool Success,
    string? Numero,
    string? Nombres,
    string? Apellidos,
    double? ConfidenceGlobal,
    string? ErrorCode,
    string? ErrorMessage);
