using System.Text;

namespace Flit.Modules.IdentityVerification.Application;

public sealed record GeneratedInvitationToken(string PlainToken, string TokenHash);

/// <summary>
/// Genera token URL-safe de un solo uso y su hash SHA-256 para persistencia.
/// </summary>
public interface IInvitationTokenGenerator
{
    GeneratedInvitationToken Generate();
}

public sealed class SecureInvitationTokenGenerator(IInvitationTokenHasher hasher) : IInvitationTokenGenerator
{
    public GeneratedInvitationToken Generate()
    {
        var bytes = new byte[32];
        System.Security.Cryptography.RandomNumberGenerator.Fill(bytes);
        var plain = Base64UrlEncode(bytes);
        var hash = hasher.HashToken(Encoding.UTF8.GetBytes(plain));
        return new GeneratedInvitationToken(plain, hash);
    }

    private static string Base64UrlEncode(ReadOnlySpan<byte> data) =>
        Convert.ToBase64String(data)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
}
