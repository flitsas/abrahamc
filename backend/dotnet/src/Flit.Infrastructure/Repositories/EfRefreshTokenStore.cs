using Microsoft.EntityFrameworkCore;
using Flit.Infrastructure.Persistence;
using Flit.Modules.Identity.Ports;

namespace Flit.Infrastructure.Repositories;

/// <summary>
/// Implementacion EF Core de IRefreshTokenStore (denylist persistida en Postgres).
/// Tabla identity.identity_refresh_tokens (ADR-0007).
/// En produccion con alta carga, sustituir por RedisRefreshTokenStore con TTL nativo.
/// </summary>
public sealed class EfRefreshTokenStore(FlitDbContext db) : IRefreshTokenStore
{
    public async Task RevokeAsync(
        Guid jti, DateTimeOffset expiresAt, string reason, CancellationToken ct)
    {
        var existing = await db.RefreshTokens.FindAsync([jti], ct);
        if (existing is null)
        {
            db.RefreshTokens.Add(new RefreshTokenEntry
            {
                Jti = jti,
                ExpiresAt = expiresAt,
                Reason = reason,
                RevokedAt = DateTimeOffset.UtcNow,
            });
            await db.SaveChangesAsync(ct);
        }
        // Si ya existe (doble revocacion), se ignora silenciosamente.
    }

    public async Task<bool> IsRevokedAsync(Guid jti, CancellationToken ct)
    {
        var entry = await db.RefreshTokens.FindAsync([jti], ct);
        if (entry is null) return false;

        // Token en la denylist pero ya expirado — limpieza best-effort
        if (entry.ExpiresAt < DateTimeOffset.UtcNow)
        {
            db.RefreshTokens.Remove(entry);
            await db.SaveChangesAsync(ct);
            return false;
        }

        return true;
    }
}
