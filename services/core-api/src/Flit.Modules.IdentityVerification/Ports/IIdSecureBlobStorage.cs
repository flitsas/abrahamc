namespace Flit.Modules.IdentityVerification.Ports;

/// <summary>
/// Almacenamiento de blobs IDSecure en MinIO (SSE-S3) o in-memory en tests.
/// </summary>
public interface IIdSecureBlobStorage
{
    Task PutEncryptedAsync(
        string bucket,
        string objectKey,
        ReadOnlyMemory<byte> content,
        string contentType,
        CancellationToken ct = default);

    Task DeleteObjectAsync(
        string bucket,
        string objectKey,
        CancellationToken ct = default);

    Task<bool> ObjectExistsAsync(
        string bucket,
        string objectKey,
        CancellationToken ct = default);

    Task<byte[]?> TryGetAsync(
        string bucket,
        string objectKey,
        CancellationToken ct = default);
}
