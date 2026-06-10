using Flit.Infrastructure.Persistence;
using Flit.Modules.Companies.Domain;
using Flit.Modules.Companies.Ports;
using Microsoft.EntityFrameworkCore;

namespace Flit.Infrastructure.Repositories;

public sealed class EfProcedureQueryResultsRepository(FlitDbContext db) : IProcedureQueryResultsRepository
{
    public async Task AddAsync(ProcedureQueryResultSnapshot snapshot, CancellationToken ct = default)
    {
        await db.ProcedureQueryResults.AddAsync(snapshot, ct);
    }
}
