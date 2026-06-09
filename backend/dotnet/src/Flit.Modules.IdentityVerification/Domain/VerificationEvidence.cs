namespace Flit.Modules.IdentityVerification.Domain;

/// <summary>
/// Evidencia biométrica. Tabla: verification_evidences.
/// </summary>
public sealed class VerificationEvidence
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid VerificationSessionId { get; private set; }
    public string EvidenceType { get; private set; } = string.Empty;
    public Guid FileId { get; private set; }
    public DateTimeOffset CapturedAt { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public Guid CreatedBy { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public Guid UpdatedBy { get; private set; }

    private VerificationEvidence() { }

    public static VerificationEvidence CreateSignatureCanvas(
        VerificationSession session,
        Guid fileId,
        DateTimeOffset now) =>
        Create(session, EvidenceTypes.SignatureCanvas, fileId, now);

    public static VerificationEvidence CreateDocumentFront(
        VerificationSession session,
        Guid fileId,
        DateTimeOffset now) =>
        Create(session, EvidenceTypes.DocumentFront, fileId, now);

    public static VerificationEvidence CreateDocumentBack(
        VerificationSession session,
        Guid fileId,
        DateTimeOffset now) =>
        Create(session, EvidenceTypes.DocumentBack, fileId, now);

    public static VerificationEvidence CreateSelfie(
        VerificationSession session,
        Guid fileId,
        DateTimeOffset now) =>
        Create(session, EvidenceTypes.Selfie, fileId, now);

    public static VerificationEvidence CreateLiveness(
        VerificationSession session,
        Guid fileId,
        DateTimeOffset now) =>
        Create(session, EvidenceTypes.Liveness, fileId, now);

    private static VerificationEvidence Create(
        VerificationSession session,
        string evidenceType,
        Guid fileId,
        DateTimeOffset now) =>
        new()
        {
            Id = Guid.CreateVersion7(),
            TenantId = session.TenantId,
            VerificationSessionId = session.Id,
            EvidenceType = evidenceType,
            FileId = fileId,
            CapturedAt = now,
            CreatedAt = now,
            CreatedBy = session.CreatedBy,
            UpdatedAt = now,
            UpdatedBy = session.CreatedBy,
        };

    /// <summary>Anonimiza evidencia tras purga de retención (HU #9490 AC1).</summary>
    public void AnonymizeAfterPurge(DateTimeOffset now, Guid actorUserId)
    {
        EvidenceType = EvidenceTypes.Purged;
        UpdatedAt = now;
        UpdatedBy = actorUserId;
    }

    public bool IsPurged => EvidenceType == EvidenceTypes.Purged;
}

public static class EvidenceTypes
{
    public const string SignatureCanvas = "signature_canvas";
    public const string Selfie = "selfie";
    public const string DocumentFront = "document_front";
    public const string DocumentBack = "document_back";
    public const string Liveness = "liveness";
    public const string Purged = "purged";
}
