namespace Flit.Modules.IdentityVerification.Ports;

/// <summary>
/// Extracción OCR documento ID (Vertex AI en prod, mock en DEV — HU #9484).
/// </summary>
public interface IIdSecureDocumentOcrProvider
{
    Task<DocumentOcrProviderResult> ExtractAsync(
        DocumentOcrRequest request,
        CancellationToken ct = default);
}

public sealed record DocumentOcrRequest(
    string FrontImageBase64,
    string BackImageBase64,
    string FrontMimeType = "image/jpeg",
    string BackMimeType = "image/jpeg");

public sealed record DocumentOcrExtractedFields(
    string FullName,
    string DocumentNumber,
    string IssueDate);

public sealed record DocumentOcrProviderResult(
    bool Success,
    DocumentOcrExtractedFields? Fields,
    IReadOnlyDictionary<string, double>? ConfidenceScores,
    string? RawProviderResponseJson,
    string? FailureCode,
    string? FailureMessage);
