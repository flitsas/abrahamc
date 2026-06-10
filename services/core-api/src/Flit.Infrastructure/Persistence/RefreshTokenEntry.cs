namespace Flit.Infrastructure.Persistence;

/// <summary>
/// Denylist de refresh tokens revocados. Persistida en Postgres con TTL gestionado
/// via job de limpieza (delete where expires_at &lt; now()).
/// Tabla: identity.identity_refresh_tokens (ADR-0007).
/// En produccion, RedisRefreshTokenStore sustituye esto con TTL nativo de Redis.
/// </summary>
public sealed class RefreshTokenEntry
{
    /// <summary>JTI (JWT ID) del refresh token — PK.</summary>
    public Guid Jti { get; set; }

    public DateTimeOffset ExpiresAt { get; set; }

    /// <summary>Razon de revocacion (logout, rotacion, etc.).</summary>
    public string Reason { get; set; } = string.Empty;

    public DateTimeOffset RevokedAt { get; set; }
}
