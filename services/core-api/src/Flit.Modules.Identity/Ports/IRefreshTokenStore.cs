namespace Flit.Modules.Identity.Ports;

/// <summary>
/// Denylist de refresh tokens revocados (ADR-0006 §"Politica de tokens").
/// Default: in-memory (MVP). Produccion: Redis con TTL = refresh expiry.
/// </summary>
public interface IRefreshTokenStore
{
    /// <summary>Revoca un refresh token (lo agrega a la denylist).</summary>
    Task RevokeAsync(Guid jti, DateTimeOffset expiresAt, string reason, CancellationToken ct);

    /// <summary>true si el jti esta en la denylist.</summary>
    Task<bool> IsRevokedAsync(Guid jti, CancellationToken ct);
}
