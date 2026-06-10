using Flit.Modules.IdentityVerification.Ports;

namespace Flit.Modules.IdentityVerification.Adapters;

/// <summary>
/// Mock Vertex OCR para DEV/tests (HU #9484). Imagen con prefijo UNREADABLE simula fallo.
/// </summary>
public sealed class MockIdSecureDocumentOcrProvider : IIdSecureDocumentOcrProvider
{
    public const string UnreadableMarker = "UNREADABLE";

    public Task<DocumentOcrProviderResult> ExtractAsync(
        DocumentOcrRequest request,
        CancellationToken ct = default)
    {
        if (request.FrontImageBase64.Contains(UnreadableMarker, StringComparison.OrdinalIgnoreCase)
            || request.BackImageBase64.Contains(UnreadableMarker, StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(new DocumentOcrProviderResult(
                Success: false,
                Fields: null,
                ConfidenceScores: null,
                RawProviderResponseJson: """{"provider":"mock_vertex","status":"failed"}""",
                FailureCode: "OCR_UNREADABLE",
                FailureMessage: "Imagen ilegible: no se pudo extraer texto del documento"));
        }

        var fields = new DocumentOcrExtractedFields(
            FullName: "María Fernanda López",
            DocumentNumber: "1020304050",
            IssueDate: "2018-07-15");

        var confidence = new Dictionary<string, double>
        {
            ["full_name"] = 0.94,
            ["document_number"] = 0.97,
            ["issue_date"] = 0.89,
        };

        const string raw = """{"provider":"mock_vertex","status":"ok","model":"document-ocr-v1"}""";

        return Task.FromResult(new DocumentOcrProviderResult(
            Success: true,
            Fields: fields,
            ConfidenceScores: confidence,
            RawProviderResponseJson: raw,
            FailureCode: null,
            FailureMessage: null));
    }
}
