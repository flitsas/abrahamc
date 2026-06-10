using Microsoft.EntityFrameworkCore;
using Flit.Infrastructure.Persistence;
using Flit.Modules.Identity.Ports;

namespace Flit.Infrastructure.Repositories;

/// <summary>
/// Implementacion EF Core de ICredentialsRepository.
/// Tabla identity.identity_credentials (ADR-0007).
/// </summary>
public sealed class EfCredentialsRepository(FlitDbContext db) : ICredentialsRepository
{
    public async Task<string?> ObtenerPasswordHashAsync(Guid userId, CancellationToken ct)
    {
        var cred = await db.Credenciales.FindAsync([userId], ct);
        return cred?.PasswordHash;
    }

    public async Task GuardarPasswordHashAsync(
        Guid userId, string passwordHash, DateTimeOffset cambiadoEn, CancellationToken ct)
    {
        var existing = await db.Credenciales.FindAsync([userId], ct);
        if (existing is null)
        {
            db.Credenciales.Add(new IdentityCredential
            {
                UserId = userId,
                PasswordHash = passwordHash,
                CambiadoEn = cambiadoEn,
            });
        }
        else
        {
            existing.PasswordHash = passwordHash;
            existing.CambiadoEn = cambiadoEn;
        }

        await db.SaveChangesAsync(ct);
    }

    public async Task EliminarAsync(Guid userId, CancellationToken ct)
    {
        var existing = await db.Credenciales.FindAsync([userId], ct);
        if (existing is not null)
        {
            db.Credenciales.Remove(existing);
            await db.SaveChangesAsync(ct);
        }
    }
}
