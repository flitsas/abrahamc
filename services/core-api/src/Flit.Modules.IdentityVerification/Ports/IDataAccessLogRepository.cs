using Flit.Modules.IdentityVerification.Domain;

namespace Flit.Modules.IdentityVerification.Ports;

public interface IDataAccessLogRepository
{
    Task AppendAsync(DataAccessLogEntry entry, CancellationToken ct);
}
