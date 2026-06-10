using Flit.Modules.IdentityVerification.Domain;
using Flit.Modules.IdentityVerification.Ports;
using Flit.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Flit.Infrastructure.Repositories;

public sealed class EfIdSecureFileRepository(FlitDbContext db) : IIdSecureFileRepository
{
    public Task AddAsync(IdSecureStoredFile file, CancellationToken ct)
    {
        db.IdSecureStoredFiles.Add(file);
        return Task.CompletedTask;
    }

    public Task<IdSecureStoredFile?> GetByIdAsync(Guid tenantId, Guid fileId, CancellationToken ct) =>
        db.IdSecureStoredFiles.FirstOrDefaultAsync(
            f => f.TenantId == tenantId && f.Id == fileId, ct);

    public Task UpdateAsync(IdSecureStoredFile file, CancellationToken ct)
    {
        db.IdSecureStoredFiles.Update(file);
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
}
