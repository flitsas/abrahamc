using System.Collections.Concurrent;
using Flit.Modules.IdentityVerification.Domain;
using Flit.Modules.IdentityVerification.Ports;

namespace Flit.Modules.IdentityVerification.Adapters;

/// <summary>
/// MinIO simulado con cifrado SSE-S3 marcado (DEV/tests — HU #9490).
/// </summary>
public sealed class InMemoryIdSecureBlobStorage : IIdSecureBlobStorage
{
    private readonly ConcurrentDictionary<(string Bucket, string Key), byte[]> _objects = new();

    public IReadOnlyDictionary<(string Bucket, string Key), byte[]> Objects => _objects;

    public Task PutEncryptedAsync(
        string bucket,
        string objectKey,
        ReadOnlyMemory<byte> content,
        string contentType,
        CancellationToken ct = default)
    {
        _objects[(bucket, objectKey)] = content.ToArray();
        return Task.CompletedTask;
    }

    public Task DeleteObjectAsync(string bucket, string objectKey, CancellationToken ct = default)
    {
        _objects.TryRemove((bucket, objectKey), out _);
        return Task.CompletedTask;
    }

    public Task<bool> ObjectExistsAsync(string bucket, string objectKey, CancellationToken ct = default) =>
        Task.FromResult(_objects.ContainsKey((bucket, objectKey)));

    public Task<byte[]?> TryGetAsync(string bucket, string objectKey, CancellationToken ct = default) =>
        Task.FromResult(_objects.TryGetValue((bucket, objectKey), out var bytes) ? bytes : null);
}
