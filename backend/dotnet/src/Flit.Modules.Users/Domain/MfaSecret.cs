namespace Flit.Modules.Users.Domain;

/// <summary>
/// Value Object que representa un secret TOTP cifrado en reposo.
/// AES-256-GCM con master key en env var (ADR-0010). El nonce de 12 bytes
/// se genera por cifrado y se almacena junto con el ciphertext.
///
/// El plaintext del secret (32 bytes base32) NUNCA debe persistirse ni loguearse.
/// </summary>
public sealed record MfaSecret(
    byte[] Ciphertext,
    byte[] Nonce,
    string KeyId)
{
    public static MfaSecret Create(byte[] ciphertext, byte[] nonce, string keyId)
    {
        if (ciphertext is null || ciphertext.Length == 0)
            throw new ArgumentException("Ciphertext requerido", nameof(ciphertext));
        if (nonce is null || nonce.Length != 12)
            throw new ArgumentException("Nonce debe ser 12 bytes (AES-GCM)", nameof(nonce));
        if (string.IsNullOrWhiteSpace(keyId))
            throw new ArgumentException("KeyId requerido", nameof(keyId));
        return new MfaSecret(ciphertext, nonce, keyId);
    }
}
