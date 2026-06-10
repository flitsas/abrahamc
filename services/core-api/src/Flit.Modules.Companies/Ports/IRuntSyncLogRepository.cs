using Flit.Modules.Companies.Domain;

namespace Flit.Modules.Companies.Ports;

public interface IRuntSyncLogRepository
{
    Task AddAsync(RuntSyncLogEntry entry, CancellationToken ct = default);
}
