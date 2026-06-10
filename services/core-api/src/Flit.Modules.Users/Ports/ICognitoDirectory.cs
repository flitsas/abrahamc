namespace Flit.Modules.Users.Ports;

/// <summary>
/// Puerto hacia Cognito. La implementacion HybridCognitoDirectoryAdapter
/// vive en Flit.Modules.Identity (Fase 7). Esta version stub permite
/// avanzar las pruebas locales sin AWS.
///
/// Todas las llamadas con timeout 5s (ADR-0010).
/// </summary>
public interface ICognitoDirectory
{
    /// <summary>
    /// Crea el usuario en Cognito y devuelve el sub generado.
    /// MessageAction=SUPPRESS (no enviar email desde Cognito).
    /// </summary>
    Task<CognitoUserResult> AdminCreateUserAsync(
        string email,
        string tempPassword,
        Guid appUserId,
        CancellationToken ct = default);

    Task AdminSetUserPasswordAsync(
        string username,
        string newPassword,
        bool permanent,
        CancellationToken ct = default);

    Task<CognitoTokens> AdminInitiateAuthAsync(
        string username,
        string password,
        CancellationToken ct = default);

    Task<CognitoTokens> AdminRefreshAuthAsync(
        string refreshToken,
        CancellationToken ct = default);

    Task AdminDisableUserAsync(string username, CancellationToken ct = default);
    Task AdminEnableUserAsync(string username, CancellationToken ct = default);
    Task AdminUserGlobalSignOutAsync(string username, CancellationToken ct = default);
    Task RevokeTokenAsync(string refreshToken, CancellationToken ct = default);
}

public sealed record CognitoUserResult(string Sub, string Username);
public sealed record CognitoTokens(
    string AccessToken,
    string IdToken,
    string? RefreshToken,
    int ExpiresIn);
