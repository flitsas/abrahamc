using Flit.Infrastructure.Persistence;
using Flit.Modules.Companies.Domain;
using Flit.Modules.Companies.Ports;
using Microsoft.EntityFrameworkCore;

namespace Flit.Infrastructure.Repositories;

public sealed class EfRuntSyncLogRepository(FlitDbContext db) : IRuntSyncLogRepository
{
    public async Task AddAsync(RuntSyncLogEntry entry, CancellationToken ct = default)
    {
        await db.RuntSyncLogs.AddAsync(entry, ct);
    }
}
