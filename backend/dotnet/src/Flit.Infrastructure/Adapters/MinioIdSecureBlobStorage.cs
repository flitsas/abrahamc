using System.Net;
using Amazon.S3;
using Amazon.S3.Model;
using Flit.Modules.IdentityVerification.Ports;

namespace Flit.Infrastructure.Adapters;

/// <summary>
/// Almacenamiento IDSecure en MinIO vía AWSSDK.S3 (ForcePathStyle — ADR-0016).
/// </summary>
public sealed class MinioIdSecureBlobStorage(IAmazonS3 s3, MinioOptions options) : IIdSecureBlobStorage
{
    public async Task PutEncryptedAsync(
        string bucket,
        string objectKey,
        ReadOnlyMemory<byte> content,
        string contentType,
        CancellationToken ct = default)
    {
        using var stream = new MemoryStream(content.ToArray(), writable: false);
        var request = new PutObjectRequest
        {
            BucketName = bucket,
            Key = objectKey,
            InputStream = stream,
            ContentType = contentType,
        };

        if (options.ServerSideEncryption)
            request.ServerSideEncryptionMethod = ServerSideEncryptionMethod.AES256;

        await s3.PutObjectAsync(request, ct);
    }

    public Task DeleteObjectAsync(
        string bucket,
        string objectKey,
        CancellationToken ct = default) =>
        s3.DeleteObjectAsync(bucket, objectKey, ct);

    public async Task<bool> ObjectExistsAsync(
        string bucket,
        string objectKey,
        CancellationToken ct = default)
    {
        try
        {
            await s3.GetObjectMetadataAsync(bucket, objectKey, ct);
            return true;
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return false;
        }
    }

    public async Task<byte[]?> TryGetAsync(
        string bucket,
        string objectKey,
        CancellationToken ct = default)
    {
        try
        {
            using var response = await s3.GetObjectAsync(bucket, objectKey, ct);
            await using var ms = new MemoryStream();
            await response.ResponseStream.CopyToAsync(ms, ct);
            return ms.ToArray();
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }
}

/// <summary>
/// Factory del cliente S3 apuntando a MinIO local o VPS.
/// </summary>
public static class MinioS3ClientFactory
{
    public static IAmazonS3 Create(MinioOptions options)
    {
        var config = new AmazonS3Config
        {
            ServiceURL = options.Endpoint.TrimEnd('/'),
            ForcePathStyle = true,
            AuthenticationRegion = options.Region,
        };

        return new AmazonS3Client(options.AccessKey, options.SecretKey, config);
    }
}
