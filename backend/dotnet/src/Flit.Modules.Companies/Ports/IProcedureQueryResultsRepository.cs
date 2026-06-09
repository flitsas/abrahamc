using Flit.Modules.Companies.Domain;

namespace Flit.Modules.Companies.Ports;

public interface IProcedureQueryResultsRepository
{
    Task AddAsync(ProcedureQueryResultSnapshot snapshot, CancellationToken ct = default);
}
