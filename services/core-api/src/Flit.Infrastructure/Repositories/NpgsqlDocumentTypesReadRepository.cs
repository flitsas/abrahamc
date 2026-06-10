using Microsoft.EntityFrameworkCore;
using Flit.Infrastructure.Persistence;
using Flit.Modules.ProceduresConfig.Ports;

namespace Flit.Infrastructure.Repositories;

public sealed class NpgsqlDocumentTypesReadRepository(FlitDbContext db) : IDocumentTypesReadRepository
{
    public async Task<DocumentTypeRecord?> GetByCodeAsync(string code, CancellationToken ct = default)
    {
        var conn = db.Database.GetDbConnection();
        if (conn.State != System.Data.ConnectionState.Open)
        {
            await conn.OpenAsync(ct);
        }

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT code, default_person_kind
            FROM catalogs.document_types
            WHERE code = @code
            LIMIT 1
            """;
        var p = cmd.CreateParameter();
        p.ParameterName = "code";
        p.Value = code;
        cmd.Parameters.Add(p);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
        {
            return null;
        }

        return new DocumentTypeRecord(
            reader.GetString(0),
            reader.IsDBNull(1) ? null : reader.GetString(1));
    }
}
