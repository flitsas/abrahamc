namespace Flit.Modules.IdentityVerification.Domain;

/// <summary>
/// Resultado OCR de documento. Tabla: verification_ocr_results (HU #9484).
/// </summary>
public sealed class VerificationOcrResult
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid VerificationSessionId { get; private set; }
    public string ExtractedFieldsJson { get; private set; } = "{}";
    public string ConfidenceScoresJson { get; private set; } = "{}";
    public string? RawProviderResponseJson { get; private set; }
    public DateTimeOffset ProcessedAt { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public Guid CreatedBy { get; private set; }

    private VerificationOcrResult() { }

    public static VerificationOcrResult Create(
        VerificationSession session,
        string extractedFieldsJson,
        string confidenceScoresJson,
        string? rawProviderResponseJson,
        DateTimeOffset now)
    {
        return new VerificationOcrResult
        {
            Id = Guid.CreateVersion7(),
            TenantId = session.TenantId,
            VerificationSessionId = session.Id,
            ExtractedFieldsJson = extractedFieldsJson,
            ConfidenceScoresJson = confidenceScoresJson,
            RawProviderResponseJson = rawProviderResponseJson,
            ProcessedAt = now,
            CreatedAt = now,
            CreatedBy = session.CreatedBy,
        };
    }

    /// <summary>Anonimiza campos OCR tras purga de retención (HU #9490 AC1).</summary>
    public void AnonymizeAfterPurge()
    {
        ExtractedFieldsJson = "{}";
        ConfidenceScoresJson = "{}";
        RawProviderResponseJson = null;
    }
}

/// <summary>Claves normalizadas en extracted_fields (AC1).</summary>
public static class OcrFieldKeys
{
    public const string FullName = "full_name";
    public const string DocumentNumber = "document_number";
    public const string IssueDate = "issue_date";
}

public static class OcrFailureMetadataKeys
{
    public const string Reason = "ocr_failure_reason";
    public const string Code = "ocr_failure_code";
}
