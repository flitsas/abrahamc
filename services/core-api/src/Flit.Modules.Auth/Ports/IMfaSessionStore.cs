using Flit.Modules.Users.Ports;

namespace Flit.Modules.Auth.Ports;

/// <summary>
/// Store de sesiones MFA pendientes (ADR-0010 §"Sesiones MFA pendientes").
///
/// Cuando un usuario con MFA habilitado pasa el primer paso (Cognito valido
/// password + emitio tokens), retenemos los tokens en este store con TTL 5min.
/// El segundo paso (POST /auth/mfa con codigo TOTP) los libera.
///
/// Impl prod: RedisMfaSessionStore (StackExchange.Redis con TTL nativo).
/// Impl dev: InMemoryMfaSessionStore (ConcurrentDictionary + timer).
/// </summary>
public interface IMfaSessionStore
{
    /// <summary>Guarda una sesion pendiente. Retorna el session_id generado.</summary>
    Task<Guid> CreateAsync(PendingMfaSession session, TimeSpan ttl, CancellationToken ct = default);

    /// <summary>Recupera la sesion. Devuelve null si no existe o expiro.</summary>
    Task<PendingMfaSession?> GetAsync(Guid sessionId, CancellationToken ct = default);

    /// <summary>Incrementa el contador de intentos fallidos. Retorna el nuevo valor.</summary>
    Task<int> IncrementAttemptsAsync(Guid sessionId, CancellationToken ct = default);

    /// <summary>Borra la sesion (al validar exitosamente o tras max-attempts).</summary>
    Task DeleteAsync(Guid sessionId, CancellationToken ct = default);
}

/// <summary>
/// Snapshot de una sesion MFA pendiente. Contiene los tokens Cognito recibidos
/// en el primer step + datos del usuario + contador de intentos.
///
/// IMPORTANTE: NO loguear este record completo (contiene access_token + refresh_token).
/// </summary>
public sealed record PendingMfaSession(
    Guid UserId,
    string CognitoSub,
    string Email,
    CognitoTokens Tokens,
    DateTimeOffset CreatedAt,
    int Attempts);
