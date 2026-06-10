using System.Collections.Concurrent;
using Flit.Modules.ProceduresConfig.Ports;

namespace Flit.Modules.ProceduresConfig.Adapters;

public sealed class InMemoryProcedurePdfFileStore : IProcedurePdfFileStore
{
    private readonly ConcurrentDictionary<Guid, byte[]> _blobs = new();

    public IReadOnlyDictionary<Guid, byte[]> Blobs => _blobs;

    public Task<StoredPdfFile> StoreAsync(
        Guid tenantId,
        byte[] pdfBytes,
        Guid actorUserId,
        CancellationToken ct = default)
    {
        var fileId = Guid.NewGuid();
        _blobs[fileId] = pdfBytes;
        var key = $"tramites/{tenantId:N}/{fileId:N}.pdf";
        return Task.FromResult(new StoredPdfFile(fileId, "tramites-generated", key, pdfBytes.LongLength));
    }

    public Task<byte[]?> TryGetAsync(Guid fileId, CancellationToken ct = default) =>
        Task.FromResult(_blobs.TryGetValue(fileId, out var bytes) ? bytes : null);
}
