using System.Security.Cryptography;
using System.Text;
using Amazon.CognitoIdentityProvider;
using Amazon.CognitoIdentityProvider.Model;
using Flit.Modules.Identity.Domain;
using Flit.Modules.Identity.Ports;
using Flit.SharedKernel;

namespace Flit.Modules.Identity.Adapters;

/// <summary>
/// AWS Cognito Identity Provider (ADR-0006 + ADR-0008 clarificacion 2026-05-21).
///
/// Patron server-to-server: usa AdminInitiateAuth y AdminCreateUser.
/// Los tokens de Cognito son INTERNOS al core-api; nunca se exponen al
/// frontend. core-api emite su propio JWT RS256 con los claims del dominio.
///
/// Habeas Data: el password viaja a us-east-1 sobre TLS 1.3. Cluster
/// Cognito multi-AZ con SOC2 Type II.
/// </summary>
public sealed class CognitoIdentityProvider : IIdentityProvider, IDisposable
{
    private readonly IAmazonCognitoIdentityProvider _cognito;
    private readonly CognitoSettings _settings;

    public string Name => "Cognito";

    public CognitoIdentityProvider(IAmazonCognitoIdentityProvider cognito, CognitoSettings settings)
    {
        _cognito = cognito;
        _settings = settings;
    }

    /// <summary>
    /// AdminInitiateAuth con flow ADMIN_USER_PASSWORD_AUTH.
    /// Si la cuenta esta UNCONFIRMED o DISABLED, devuelve CredencialesInvalidas
    /// para no revelar estado (no enumeration).
    /// </summary>
    public async Task<Result<Unit, IdentityError>> VerifyCredentialsAsync(
        string email, string password, CancellationToken ct)
    {
        try
        {
            var authParams = new Dictionary<string, string>
            {
                ["USERNAME"] = email,
                ["PASSWORD"] = password,
            };
            if (!string.IsNullOrEmpty(_settings.AppClientSecret))
                authParams["SECRET_HASH"] = ComputeSecretHash(email);

            var resp = await _cognito.AdminInitiateAuthAsync(new AdminInitiateAuthRequest
            {
                UserPoolId = _settings.UserPoolId,
                ClientId = _settings.AppClientId,
                AuthFlow = AuthFlowType.ADMIN_USER_PASSWORD_AUTH,
                AuthParameters = authParams,
            }, ct).ConfigureAwait(false);

            // Si Cognito requiere challenge (FORCE_CHANGE_PASSWORD, MFA_SETUP,
            // SMS_MFA, SOFTWARE_TOKEN_MFA), el MVP devuelve credenciales
            // invalidas. Post-MVP: manejar challenges con flow propio.
            if (resp.ChallengeName is not null)
                return Result<Unit, IdentityError>.Failure(new IdentityError.CredencialesInvalidas());

            return Result<Unit, IdentityError>.Success(Unit.Value);
        }
        catch (NotAuthorizedException)
        {
            return Result<Unit, IdentityError>.Failure(new IdentityError.CredencialesInvalidas());
        }
        catch (UserNotFoundException)
        {
            // No revelar (prevent_user_existence_errors=ENABLED en App Client
            // ya esconde esto, pero defensa en profundidad).
            return Result<Unit, IdentityError>.Failure(new IdentityError.CredencialesInvalidas());
        }
        catch (UserNotConfirmedException)
        {
            return Result<Unit, IdentityError>.Failure(new IdentityError.CredencialesInvalidas());
        }
        catch (TooManyRequestsException)
        {
            // Rate limit de Cognito alcanzado: mejor enmascarar como
            // credenciales invalidas (no facilitar enumeration por DoS).
            return Result<Unit, IdentityError>.Failure(new IdentityError.CredencialesInvalidas());
        }
    }

    public async Task<Result<Unit, IdentityError>> CreateCredentialAsync(
        Guid userId, string email, string password, CancellationToken ct)
    {
        try
        {
            // 1. AdminCreateUser (sin enviar email de bienvenida; lo maneja
            //    core-api con su propia plantilla).
            await _cognito.AdminCreateUserAsync(new AdminCreateUserRequest
            {
                UserPoolId = _settings.UserPoolId,
                Username = email,
                MessageAction = MessageActionType.SUPPRESS,
                UserAttributes =
                [
                    new AttributeType { Name = "email", Value = email },
                    new AttributeType { Name = "email_verified", Value = "true" },
                    new AttributeType { Name = "custom:rol", Value = "ciudadano" },
                ],
            }, ct).ConfigureAwait(false);

            // 2. AdminSetUserPassword permanent=true (skip FORCE_CHANGE_PASSWORD).
            await _cognito.AdminSetUserPasswordAsync(new AdminSetUserPasswordRequest
            {
                UserPoolId = _settings.UserPoolId,
                Username = email,
                Password = password,
                Permanent = true,
            }, ct).ConfigureAwait(false);

            return Result<Unit, IdentityError>.Success(Unit.Value);
        }
        catch (UsernameExistsException)
        {
            return Result<Unit, IdentityError>.Failure(new IdentityError.EmailDuplicado(email));
        }
        catch (InvalidPasswordException ex)
        {
            return Result<Unit, IdentityError>.Failure(
                new IdentityError.PasswordInvalido(ex.Message));
        }
    }

    public async Task<Result<Unit, IdentityError>> DeleteCredentialAsync(
        Guid userId, string email, CancellationToken ct)
    {
        try
        {
            await _cognito.AdminDeleteUserAsync(new AdminDeleteUserRequest
            {
                UserPoolId = _settings.UserPoolId,
                Username = email,
            }, ct).ConfigureAwait(false);
            return Result<Unit, IdentityError>.Success(Unit.Value);
        }
        catch (UserNotFoundException)
        {
            // Idempotente: ya no existe.
            return Result<Unit, IdentityError>.Success(Unit.Value);
        }
    }

    public async Task SignOutAsync(string email, CancellationToken ct)
    {
        try
        {
            await _cognito.AdminUserGlobalSignOutAsync(new AdminUserGlobalSignOutRequest
            {
                UserPoolId = _settings.UserPoolId,
                Username = email,
            }, ct).ConfigureAwait(false);
        }
        catch (UserNotFoundException)
        {
            // No-op idempotente.
        }
    }

    /// <summary>
    /// Cognito SECRET_HASH = Base64(HMAC-SHA256(clientSecret, username + clientId))
    /// Obligatorio cuando el App Client tiene client_secret.
    /// </summary>
    private string ComputeSecretHash(string username)
    {
        var message = Encoding.UTF8.GetBytes(username + _settings.AppClientId);
        var key = Encoding.UTF8.GetBytes(_settings.AppClientSecret);
        using var hmac = new HMACSHA256(key);
        return Convert.ToBase64String(hmac.ComputeHash(message));
    }

    public void Dispose() => _cognito.Dispose();
}

/// <summary>Settings de Cognito inyectados via Options pattern.</summary>
public sealed record CognitoSettings(
    string UserPoolId,
    string AppClientId,
    string AppClientSecret);
