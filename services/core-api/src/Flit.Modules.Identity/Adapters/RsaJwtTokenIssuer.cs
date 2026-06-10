using System.Security.Cryptography;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Flit.Modules.Identity.Domain;
using Flit.Modules.Identity.Ports;
using Flit.SharedKernel;

namespace Flit.Modules.Identity.Adapters;

/// <summary>
/// Token issuer JWT RS256 (ADR-0006 §"Politica de tokens").
/// La llave privada vive SOLO aqui (core-api). Los demas servicios validan
/// con la llave publica via JWKS o llave montada.
///
/// MVP: usa System.IdentityModel.Tokens.Jwt en vez de OpenIddict 6.x por
/// el riesgo R1 (AOT + OpenIddict spike pendiente). OpenIddict se evalua
/// post-MVP.
/// </summary>
public sealed class RsaJwtTokenIssuer : ITokenIssuer, IDisposable
{
    private readonly RSA _privateKey;
    private readonly SigningCredentials _signingCredentials;
    private readonly TokenValidationParameters _validationParams;
    private readonly string _issuer;
    private readonly string _audience;
    private readonly TimeSpan _accessTtl;
    private readonly TimeSpan _refreshTtl;
    private readonly IClock _clock;
    private readonly JsonWebTokenHandler _handler = new();

    public RsaJwtTokenIssuer(JwtSettings settings, IClock clock)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(clock);

        _privateKey = RSA.Create();
        _privateKey.ImportFromPem(settings.PrivateKeyPem);

        var publicKey = RSA.Create();
        publicKey.ImportFromPem(settings.PublicKeyPem);

        var securityKey = new RsaSecurityKey(_privateKey);
        _signingCredentials = new SigningCredentials(securityKey, SecurityAlgorithms.RsaSha256);

        _validationParams = new TokenValidationParameters
        {
            ValidIssuer = settings.Issuer,
            ValidAudience = settings.Audience,
            IssuerSigningKey = new RsaSecurityKey(publicKey),
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateIssuerSigningKey = true,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30),
            ValidAlgorithms = [SecurityAlgorithms.RsaSha256],
        };

        _issuer = settings.Issuer;
        _audience = settings.Audience;
        _accessTtl = settings.AccessTtl;
        _refreshTtl = settings.RefreshTtl;
        _clock = clock;
    }

    public TokenPair Issue(Usuario usuario)
    {
        ArgumentNullException.ThrowIfNull(usuario);

        var now = _clock.UtcNow;
        var accessExp = now.Add(_accessTtl);
        var refreshExp = now.Add(_refreshTtl);

        var accessJti = Guid.CreateVersion7();
        var refreshJti = Guid.CreateVersion7();

        var accessClaims = new Dictionary<string, object>
        {
            ["sub"] = usuario.Id.ToString(),
            ["email"] = usuario.Email.Value,
            ["rol"] = usuario.Rol.ToClaimValue(),
            ["jti"] = accessJti.ToString(),
        };

        if (usuario.OrganismoId.HasValue)
        {
            accessClaims["organismo_id"] = usuario.OrganismoId.Value.ToString();
        }

        var accessToken = _handler.CreateToken(new SecurityTokenDescriptor
        {
            Claims = accessClaims,
            Issuer = _issuer,
            Audience = _audience,
            IssuedAt = now.UtcDateTime,
            NotBefore = now.UtcDateTime,
            Expires = accessExp.UtcDateTime,
            SigningCredentials = _signingCredentials,
        });

        var refreshClaims = new Dictionary<string, object>
        {
            ["sub"] = usuario.Id.ToString(),
            ["jti"] = refreshJti.ToString(),
            ["typ"] = "refresh",
        };

        var refreshToken = _handler.CreateToken(new SecurityTokenDescriptor
        {
            Claims = refreshClaims,
            Issuer = _issuer,
            Audience = _audience,
            IssuedAt = now.UtcDateTime,
            NotBefore = now.UtcDateTime,
            Expires = refreshExp.UtcDateTime,
            SigningCredentials = _signingCredentials,
        });

        return new TokenPair(accessToken, refreshToken, accessExp, refreshExp);
    }

    public (string AccessToken, DateTimeOffset AccessExpiresAt) IssueTramitesAccess(TramitesSessionSubject subject)
    {
        ArgumentNullException.ThrowIfNull(subject);

        var now = _clock.UtcNow;
        var accessExp = now.Add(_accessTtl);

        var accessClaims = new Dictionary<string, object>
        {
            ["sub"] = subject.UserId.ToString(),
            ["email"] = subject.Email,
            ["tenant_id"] = subject.TenantId.ToString(),
            ["is_super_admin"] = subject.IsSuperAdmin ? "true" : "false",
            ["roles"] = string.Join(',', subject.RoleSlugs),
            ["permissions"] = string.Join(',', subject.PermissionSlugs),
            ["permissions_epoch"] = subject.PermissionsEpoch,
            ["jti"] = Guid.CreateVersion7().ToString(),
        };

        var accessToken = _handler.CreateToken(new SecurityTokenDescriptor
        {
            Claims = accessClaims,
            Issuer = _issuer,
            Audience = _audience,
            IssuedAt = now.UtcDateTime,
            NotBefore = now.UtcDateTime,
            Expires = accessExp.UtcDateTime,
            SigningCredentials = _signingCredentials,
        });

        return (accessToken, accessExp);
    }

    public TramitesSessionSubject? ValidateTramitesAccess(string accessToken)
    {
        if (string.IsNullOrWhiteSpace(accessToken))
            return null;

        var validation = _handler.ValidateTokenAsync(accessToken, _validationParams)
            .GetAwaiter().GetResult();
        if (!validation.IsValid)
            return null;

        if (!validation.Claims.TryGetValue("sub", out var subObj) ||
            !Guid.TryParse(subObj?.ToString(), out var userId))
            return null;

        if (!validation.Claims.TryGetValue("tenant_id", out var tenantObj) ||
            !Guid.TryParse(tenantObj?.ToString(), out var tenantId))
            return null;

        var email = validation.Claims.TryGetValue("email", out var emailObj)
            ? emailObj?.ToString() ?? string.Empty
            : string.Empty;

        var isSuperAdmin = validation.Claims.TryGetValue("is_super_admin", out var saObj) &&
            string.Equals(saObj?.ToString(), "true", StringComparison.OrdinalIgnoreCase);

        var permsRaw = validation.Claims.TryGetValue("permissions", out var permsObj)
            ? permsObj?.ToString()
            : null;
        var slugs = string.IsNullOrWhiteSpace(permsRaw)
            ? Array.Empty<string>()
            : permsRaw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        var rolesRaw = validation.Claims.TryGetValue("roles", out var rolesObj)
            ? rolesObj?.ToString()
            : null;
        var roles = string.IsNullOrWhiteSpace(rolesRaw)
            ? Array.Empty<string>()
            : rolesRaw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        var epoch = validation.Claims.TryGetValue("permissions_epoch", out var epochObj) &&
            int.TryParse(epochObj?.ToString(), out var parsedEpoch)
            ? parsedEpoch
            : 1;

        return new TramitesSessionSubject(
            userId, tenantId, email, isSuperAdmin, roles, slugs, epoch);
    }

    public Guid? ValidateRefresh(string refreshToken)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
            return null;

        var validation = _handler.ValidateTokenAsync(refreshToken, _validationParams).GetAwaiter().GetResult();
        if (!validation.IsValid)
            return null;

        if (!validation.Claims.TryGetValue("typ", out var typ) ||
            typ?.ToString() != "refresh")
            return null;

        if (!validation.Claims.TryGetValue("sub", out var sub))
            return null;

        return Guid.TryParse(sub.ToString(), out var userId) ? userId : null;
    }

    /// <summary>Extrae el jti del refresh token (sin validar). Para revocacion.</summary>
    public static Guid? ExtractRefreshJti(string refreshToken)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
            return null;
        try
        {
            var handler = new JsonWebTokenHandler();
            var token = handler.ReadJsonWebToken(refreshToken);
            return Guid.TryParse(token.Id, out var jti) ? jti : null;
        }
        catch (Exception)
        {
            return null;
        }
    }

    public void Dispose() => _privateKey.Dispose();
}

/// <summary>Configuracion del token issuer. Inyectada por DI (Options pattern).</summary>
public sealed record JwtSettings(
    string PrivateKeyPem,
    string PublicKeyPem,
    string Issuer,
    string Audience,
    TimeSpan AccessTtl,
    TimeSpan RefreshTtl);
