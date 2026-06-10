using System.Data;
using Npgsql;
using NpgsqlTypes;
using Microsoft.EntityFrameworkCore;
using Flit.Infrastructure.Persistence;
using Flit.Modules.ProceduresConfig.Ports;

namespace Flit.Infrastructure.Repositories;

/// <summary>Persiste metadatos en <c>files.files</c>; binario en memoria del proceso hasta integración MinIO dedicada.</summary>
public sealed class NpgsqlProcedurePdfFileStore(FlitDbContext db) : IProcedurePdfFileStore
{
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, byte[]> BlobCache = new();

    public async Task<StoredPdfFile> StoreAsync(
        Guid tenantId,
        byte[] pdfBytes,
        Guid actorUserId,
        CancellationToken ct = default)
    {
        var fileId = Guid.NewGuid();
        var bucket = "tramites-generated";
        var objectKey = $"tramites/{tenantId:N}/{fileId:N}.pdf";
        BlobCache[objectKey] = pdfBytes;

        var conn = (NpgsqlConnection)db.Database.GetDbConnection();
        if (conn.State != ConnectionState.Open)
            await conn.OpenAsync(ct);

        await TenantDbContext.SetTenantContextAsync(conn, tenantId, ct);

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO files.files (
              id, tenant_id, bucket, object_key, content_type, size_bytes, status,
              created_by, updated_by
            ) VALUES (
              @id, @tenant_id, @bucket, @object_key, 'application/pdf', @size_bytes, 'ready',
              @actor, @actor
            )
            """;
        cmd.Parameters.Add(new NpgsqlParameter("id", NpgsqlDbType.Uuid) { Value = fileId });
        cmd.Parameters.Add(new NpgsqlParameter("tenant_id", NpgsqlDbType.Uuid) { Value = tenantId });
        cmd.Parameters.Add(new NpgsqlParameter("bucket", NpgsqlDbType.Text) { Value = bucket });
        cmd.Parameters.Add(new NpgsqlParameter("object_key", NpgsqlDbType.Text) { Value = objectKey });
        cmd.Parameters.Add(new NpgsqlParameter("size_bytes", NpgsqlDbType.Bigint) { Value = pdfBytes.LongLength });
        cmd.Parameters.Add(new NpgsqlParameter("actor", NpgsqlDbType.Uuid) { Value = actorUserId });
        await cmd.ExecuteNonQueryAsync(ct);

        return new StoredPdfFile(fileId, bucket, objectKey, pdfBytes.LongLength);
    }

    public async Task<byte[]?> TryGetAsync(Guid fileId, CancellationToken ct = default)
    {
        var conn = (NpgsqlConnection)db.Database.GetDbConnection();
        if (conn.State != ConnectionState.Open)
            await conn.OpenAsync(ct);

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT object_key
            FROM files.files
            WHERE id = @id AND deleted_at IS NULL
            """;
        cmd.Parameters.Add(new NpgsqlParameter("id", NpgsqlDbType.Uuid) { Value = fileId });
        var objectKey = await cmd.ExecuteScalarAsync(ct) as string;
        if (string.IsNullOrEmpty(objectKey))
            return null;

        return BlobCache.TryGetValue(objectKey, out var bytes) ? bytes : null;
    }
}
