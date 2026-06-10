namespace Flit.Modules.ProceduresConfig.Ports;

/// <summary>Persiste PDF generados en <c>files.files</c> + almacenamiento (MinIO o memoria).</summary>
public interface IProcedurePdfFileStore
{
    Task<StoredPdfFile> StoreAsync(
        Guid tenantId,
        byte[] pdfBytes,
        Guid actorUserId,
        CancellationToken ct = default);

    Task<byte[]?> TryGetAsync(Guid fileId, CancellationToken ct = default);
}

public sealed record StoredPdfFile(Guid FileId, string Bucket, string ObjectKey, long SizeBytes);
