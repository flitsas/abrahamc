namespace Flit.Modules.IdentityVerification.Application;

/// <summary>
/// Genera hash SHA-256 del token de invitacion (nunca persistir token en claro).
/// </summary>
public interface IInvitationTokenHasher
{
    string HashToken(ReadOnlySpan<byte> tokenBytes);
}

public sealed class Sha256InvitationTokenHasher : IInvitationTokenHasher
{
    public string HashToken(ReadOnlySpan<byte> tokenBytes) =>
        Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(tokenBytes))
            .ToLowerInvariant();
}
