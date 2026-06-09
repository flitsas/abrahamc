using Flit.Modules.IdentityVerification.Domain;

namespace Flit.Modules.IdentityVerification.Ports;

public interface IIdSecureFileRepository
{
    Task AddAsync(IdSecureStoredFile file, CancellationToken ct);

    Task<IdSecureStoredFile?> GetByIdAsync(Guid tenantId, Guid fileId, CancellationToken ct);

    Task UpdateAsync(IdSecureStoredFile file, CancellationToken ct);

    Task SaveChangesAsync(CancellationToken ct);
}
