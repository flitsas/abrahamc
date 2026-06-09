using Flit.Modules.Identity.Domain;

namespace Flit.Modules.Identity.Ports;

/// <summary>
/// Port para emitir tokens JWT RS256 (ADR-0006 §"Politica de tokens").
/// La llave privada vive solo en core-api.
/// </summary>
public interface ITokenIssuer
{
    /// <summary>Emite access token (15 min) + refresh token (7 dias).</summary>
    TokenPair Issue(Usuario usuario);

    /// <summary>Emite JWT de acceso para sesión Trámites 2.0 (#9415).</summary>
    (string AccessToken, DateTimeOffset AccessExpiresAt) IssueTramitesAccess(TramitesSessionSubject subject);

    /// <summary>Valida access JWT y devuelve el subject o null.</summary>
    TramitesSessionSubject? ValidateTramitesAccess(string accessToken);

    /// <summary>Valida un refresh token JWT y devuelve el sub (userId) o null si invalido.</summary>
    Guid? ValidateRefresh(string refreshToken);
}

public sealed record TokenPair(
    string AccessToken,
    string RefreshToken,
    DateTimeOffset AccessExpiresAt,
    DateTimeOffset RefreshExpiresAt);
