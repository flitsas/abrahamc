namespace Flit.Modules.IdentityVerification.Domain;

/// <summary>
/// Registro en files.files para evidencias IDSecure (HU #9483 AC2).
/// </summary>
public sealed class IdSecureStoredFile
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public string Bucket { get; private set; } = string.Empty;
    public string ObjectKey { get; private set; } = string.Empty;
    public string OriginalFilename { get; private set; } = string.Empty;
    public string ContentType { get; private set; } = string.Empty;
    public long SizeBytes { get; private set; }
    public string Status { get; private set; } = FileStatuses.Ready;
    public string StorageEncryption { get; private set; } = StorageEncryptionModes.SseS3;
    public DateTimeOffset CreatedAt { get; private set; }
    public Guid CreatedBy { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public Guid UpdatedBy { get; private set; }

    private IdSecureStoredFile() { }

    public static IdSecureStoredFile CreateSignatureCanvas(
        Guid tenantId,
        Guid sessionId,
        ReadOnlySpan<byte> pngBytes,
        Guid actorUserId,
        DateTimeOffset now) =>
        CreateEvidenceImage(
            tenantId,
            sessionId,
            "signature",
            "signature.png",
            "image/png",
            pngBytes,
            actorUserId,
            now);

    public static IdSecureStoredFile CreateDocumentFront(
        Guid tenantId,
        Guid sessionId,
        ReadOnlySpan<byte> jpegBytes,
        Guid actorUserId,
        DateTimeOffset now) =>
        CreateEvidenceImage(
            tenantId,
            sessionId,
            "document-front",
            "document-front.jpg",
            "image/jpeg",
            jpegBytes,
            actorUserId,
            now);

    public static IdSecureStoredFile CreateDocumentBack(
        Guid tenantId,
        Guid sessionId,
        ReadOnlySpan<byte> jpegBytes,
        Guid actorUserId,
        DateTimeOffset now) =>
        CreateEvidenceImage(
            tenantId,
            sessionId,
            "document-back",
            "document-back.jpg",
            "image/jpeg",
            jpegBytes,
            actorUserId,
            now);

    public static IdSecureStoredFile CreateSelfie(
        Guid tenantId,
        Guid sessionId,
        ReadOnlySpan<byte> jpegBytes,
        Guid actorUserId,
        DateTimeOffset now) =>
        CreateEvidenceImage(
            tenantId,
            sessionId,
            "selfie",
            "selfie.jpg",
            "image/jpeg",
            jpegBytes,
            actorUserId,
            now);

    public static IdSecureStoredFile CreateLiveness(
        Guid tenantId,
        Guid sessionId,
        ReadOnlySpan<byte> jpegBytes,
        Guid actorUserId,
        DateTimeOffset now) =>
        CreateEvidenceImage(
            tenantId,
            sessionId,
            "liveness",
            "liveness.jpg",
            "image/jpeg",
            jpegBytes,
            actorUserId,
            now);

    private static IdSecureStoredFile CreateEvidenceImage(
        Guid tenantId,
        Guid sessionId,
        string objectPrefix,
        string originalFilename,
        string contentType,
        ReadOnlySpan<byte> bytes,
        Guid actorUserId,
        DateTimeOffset now)
    {
        var fileId = Guid.CreateVersion7();
        var extension = contentType == "image/png" ? "png" : "jpg";
        return new IdSecureStoredFile
        {
            Id = fileId,
            TenantId = tenantId,
            Bucket = IdSecureFileBuckets.IdSecure,
            ObjectKey = $"{tenantId:N}/{sessionId:N}/{objectPrefix}-{fileId:N}.{extension}",
            OriginalFilename = originalFilename,
            ContentType = contentType,
            SizeBytes = bytes.Length,
            Status = FileStatuses.Ready,
            StorageEncryption = StorageEncryptionModes.SseS3,
            CreatedAt = now,
            CreatedBy = actorUserId,
            UpdatedAt = now,
            UpdatedBy = actorUserId,
        };
    }

    /// <summary>Anonimiza metadatos tras purga de retención (HU #9490 AC1).</summary>
    public void AnonymizeAfterPurge(DateTimeOffset now, Guid actorUserId)
    {
        ObjectKey = $"redacted/{Id:N}";
        OriginalFilename = "[purged]";
        ContentType = "application/octet-stream";
        SizeBytes = 0;
        Status = FileStatuses.Purged;
        UpdatedAt = now;
        UpdatedBy = actorUserId;
    }
}

public static class IdSecureFileBuckets
{
    public const string IdSecure = "idsecure";
}

public static class FileStatuses
{
    public const string Ready = "ready";
    public const string Purged = "purged";
}

public static class StorageEncryptionModes
{
    public const string SseS3 = "sse_s3";
}
