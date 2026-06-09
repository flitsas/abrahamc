using Flit.Modules.IdentityVerification.Ports;
using Flit.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Flit.Infrastructure.Repositories;

public sealed class EfIdSecureCatalogLookup(FlitDbContext db) : IIdSecureCatalogLookup
{
    public async Task<Guid?> GetDocumentTypeIdByCodeAsync(string code, CancellationToken ct = default)
    {
        var rows = await db.Database
            .SqlQuery<Guid>($"""
                SELECT id
                FROM catalogs.document_types
                WHERE code = {code}
                LIMIT 1
                """)
            .ToListAsync(ct);

        return rows.Count > 0 ? rows[0] : null;
    }
}
