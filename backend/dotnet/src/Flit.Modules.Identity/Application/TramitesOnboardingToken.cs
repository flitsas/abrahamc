using System.Security.Cryptography;
using System.Text;

namespace Flit.Modules.Identity.Application;

/// <summary>Token opaco + firma HMAC para enlaces de onboarding 24h (HU #9418, RF-4.1).</summary>
public static class TramitesOnboardingToken
{
    public static string GenerateToken() =>
        TramitesAuthUseCases.GenerateOpaqueRefreshToken();

    public static string HashToken(string raw) =>
        TramitesAuthUseCases.HashRefreshToken(raw);

    public static string ComputeSignature(
        Guid invitationId,
        string token,
        DateTimeOffset expiresAt,
        byte[] signingKey)
    {
        var payload = $"{invitationId:N}|{token}|{expiresAt:O}";
        var hash = HMACSHA256.HashData(signingKey, Encoding.UTF8.GetBytes(payload));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    public static bool VerifySignature(
        Guid invitationId,
        string token,
        DateTimeOffset expiresAt,
        string providedSignature,
        byte[] signingKey)
    {
        if (string.IsNullOrWhiteSpace(providedSignature))
            return false;

        var expected = ComputeSignature(invitationId, token, expiresAt, signingKey);
        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(expected),
            Encoding.UTF8.GetBytes(providedSignature.Trim().ToLowerInvariant()));
    }

    public static string BuildActivationUrl(
        string baseUrl,
        Guid invitationId,
        string token,
        string signature)
    {
        var root = baseUrl.TrimEnd('/');
        var query = $"invitationId={invitationId}&token={Uri.EscapeDataString(token)}&signature={signature}";
        return $"{root}/activate?{query}";
    }
}
