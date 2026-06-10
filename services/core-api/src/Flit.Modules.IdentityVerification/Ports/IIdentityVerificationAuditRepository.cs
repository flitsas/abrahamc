using Flit.Modules.IdentityVerification.Domain;

namespace Flit.Modules.IdentityVerification.Ports;

public interface IIdentityVerificationAuditRepository
{
    Task AppendAsync(IdentityVerificationAuditEntry entry, CancellationToken ct);
}
