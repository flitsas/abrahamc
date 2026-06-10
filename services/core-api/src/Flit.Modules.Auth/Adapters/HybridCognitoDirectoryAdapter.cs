using Flit.Modules.Users.Ports;

namespace Flit.Modules.Auth.Adapters;

/// <summary>
/// Adapter "hibrido" del puerto ICognitoDirectory (FLIT 2.0 Fase 7 — ADR-0010).
///
/// Patron hibrido (prompt-cognito-hibrido.md):
///   - Cognito es la fuente de verdad de la identidad (sub, email, password)
///   - BD local es la fuente de verdad del dominio (status, profile, MFA, roles)
///   - Las operaciones write se aplican primero en BD (local-first) y luego en
///     Cognito; si Cognito falla, el caller debe compensar (rollback BD)
///
/// Esta version es un STUB con TODO para integrar AWS SDK
/// (Amazon.CognitoIdentityProvider) en la siguiente iteracion. Devuelve datos
/// deterministas para permitir pruebas E2E del flujo Auth + MFA sin AWS.
///
/// Pendiente:
///   - Reemplazar bodies por llamadas reales (AdminCreateUserAsync, AdminInitiateAuthAsync, ...)
///   - Validar IdToken con JWKS (en el JwtBearer middleware, NO aqui)
///   - Mapear errores AWS (UserNotFoundException, NotAuthorizedException, ...) a excepciones tipadas
///   - Timeouts 5s + retry policy (Polly)
///
/// La instancia se selecciona via DI cuando Identity:Provider=HybridCognito en appsettings.
/// Si Identity:Provider=Stub (default dev), se usa StubCognitoDirectory de Users.Adapters.
/// </summary>
public sealed class HybridCognitoDirectoryAdapter : ICognitoDirectory
{
    private readonly string _userPoolId;
    private readonly string _appClientId;
    private readonly string _appClientSecret;

    public HybridCognitoDirectoryAdapter(string userPoolId, string appClientId, string appClientSecret)
    {
        _userPoolId = userPoolId;
        _appClientId = appClientId;
        _appClientSecret = appClientSecret;
    }

    // -----------------------------------------------------------------------
    // TODO Fase 7.1: reemplazar todos los bodies por AWS SDK
    // -----------------------------------------------------------------------

    public Task<CognitoUserResult> AdminCreateUserAsync(
        string email, string tempPassword, Guid appUserId, CancellationToken ct = default)
    {
        // TODO: AdminCreateUserRequest con UserAttributes:
        //   email, email_verified=true, custom:app_user_id=appUserId, MessageAction=SUPPRESS
        // Luego AdminSetUserPassword(permanent=false) para forzar cambio en el primer login.
        return Task.FromResult(new CognitoUserResult(
            Sub: $"cog-{Guid.CreateVersion7()}",
            Username: email));
    }

    public Task AdminSetUserPasswordAsync(
        string username, string newPassword, bool permanent, CancellationToken ct = default)
    {
        // TODO: cognitoClient.AdminSetUserPasswordAsync(...)
        return Task.CompletedTask;
    }

    public Task<CognitoTokens> AdminInitiateAuthAsync(
        string username, string password, CancellationToken ct = default)
    {
        // TODO: AdminInitiateAuthRequest con AuthFlow=ADMIN_USER_PASSWORD_AUTH +
        //   SECRET_HASH si el app client tiene secret.
        // Lanza NotAuthorizedException si las credenciales son invalidas (caller lo mapea
        // a LoginError.InvalidCredentials por el match en el mensaje).
        return Task.FromResult(new CognitoTokens(
            AccessToken: $"hyb-access-{Guid.CreateVersion7()}",
            IdToken: $"hyb-id-{Guid.CreateVersion7()}",
            RefreshToken: $"hyb-refresh-{Guid.CreateVersion7()}",
            ExpiresIn: 3600));
    }

    public Task<CognitoTokens> AdminRefreshAuthAsync(
        string refreshToken, CancellationToken ct = default)
    {
        // TODO: AdminInitiateAuthRequest con AuthFlow=REFRESH_TOKEN_AUTH
        return Task.FromResult(new CognitoTokens(
            AccessToken: $"hyb-access-{Guid.CreateVersion7()}",
            IdToken: $"hyb-id-{Guid.CreateVersion7()}",
            RefreshToken: refreshToken,
            ExpiresIn: 3600));
    }

    public Task AdminDisableUserAsync(string username, CancellationToken ct = default)
        => Task.CompletedTask; // TODO

    public Task AdminEnableUserAsync(string username, CancellationToken ct = default)
        => Task.CompletedTask; // TODO

    public Task AdminUserGlobalSignOutAsync(string username, CancellationToken ct = default)
        => Task.CompletedTask; // TODO

    public Task RevokeTokenAsync(string refreshToken, CancellationToken ct = default)
        => Task.CompletedTask; // TODO RevokeTokenRequest con ClientId + ClientSecret

    /// <summary>Settings injectados desde appsettings (seccion HybridCognito).</summary>
    public sealed record Settings(string UserPoolId, string AppClientId, string AppClientSecret);
}
