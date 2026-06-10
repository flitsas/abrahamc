using System.Security.Cryptography;
using Flit.Modules.Users.Domain;

namespace Flit.Modules.Auth.Domain;

/// <summary>
/// Cifra/descifra el TOTP secret antes de persistirlo en identity.users (ADR-0010).
///
/// AES-256-GCM:
///   - Key: 32 bytes (master key en env MFA_MASTER_KEY_BASE64, rotable mensual)
///   - Nonce: 12 bytes random unico por cifrado
///   - Tag: 16 bytes auth tag (concatenado al ciphertext en la salida)
///
/// La master key se rota con un KeyId versionable (v1, v2, ...). Al rotar:
///   1. Generar nueva master key
///   2. Para cada user con MfaSecret, descifrar con key vieja + cifrar con
///      nueva, actualizando MfaSecret.KeyId
///   3. Eliminar la key vieja del entorno
///
/// El plaintext del secret NUNCA se persiste ni se loguea (Habeas Data).
/// </summary>
public sealed class Aes256GcmSecretCipher
{
    private readonly byte[] _masterKey;
    private readonly string _keyId;

    public Aes256GcmSecretCipher(byte[] masterKey, string keyId)
    {
        ArgumentNullException.ThrowIfNull(masterKey);
        if (masterKey.Length != 32)
            throw new ArgumentException("Master key debe ser 32 bytes (256 bits)", nameof(masterKey));
        if (string.IsNullOrWhiteSpace(keyId))
            throw new ArgumentException("KeyId requerido", nameof(keyId));
        _masterKey = masterKey;
        _keyId = keyId;
    }

    /// <summary>Construye desde la env var MFA_MASTER_KEY_BASE64 (32 bytes base64).</summary>
    public static Aes256GcmSecretCipher FromEnv(string base64MasterKey, string keyId)
    {
        if (string.IsNullOrWhiteSpace(base64MasterKey))
            throw new ArgumentException("MFA_MASTER_KEY_BASE64 vacia", nameof(base64MasterKey));
        var key = Convert.FromBase64String(base64MasterKey);
        return new Aes256GcmSecretCipher(key, keyId);
    }

    /// <summary>
    /// Cifra el secret TOTP (plaintext en base32). Devuelve MfaSecret con
    /// ciphertext + nonce + key_id listos para persistir.
    /// </summary>
    public MfaSecret Encrypt(string secretBase32)
    {
        if (string.IsNullOrWhiteSpace(secretBase32))
            throw new ArgumentException("Secret requerido", nameof(secretBase32));

        var plaintext = System.Text.Encoding.ASCII.GetBytes(secretBase32);
        var nonce = new byte[12];
        RandomNumberGenerator.Fill(nonce);

        var ciphertext = new byte[plaintext.Length];
        var tag = new byte[16];

        using var gcm = new AesGcm(_masterKey, tagSizeInBytes: 16);
        gcm.Encrypt(nonce, plaintext, ciphertext, tag);

        // Concatenar [ciphertext | tag] para persistencia atomica.
        var output = new byte[ciphertext.Length + tag.Length];
        Buffer.BlockCopy(ciphertext, 0, output, 0, ciphertext.Length);
        Buffer.BlockCopy(tag, 0, output, ciphertext.Length, tag.Length);

        return MfaSecret.Create(output, nonce, _keyId);
    }

    /// <summary>
    /// Descifra el MfaSecret persistido. Lanza CryptographicException si el
    /// auth tag no valida (tampering).
    /// </summary>
    public string Decrypt(MfaSecret secret)
    {
        ArgumentNullException.ThrowIfNull(secret);
        if (secret.KeyId != _keyId)
            throw new InvalidOperationException(
                $"Secret cifrado con key_id={secret.KeyId} pero esta instancia usa {_keyId}. " +
                "Necesitas un cipher con la key correspondiente o re-encriptar.");

        var combined = secret.Ciphertext;
        if (combined.Length < 17)
            throw new CryptographicException("Ciphertext muy corto");

        var cipherLen = combined.Length - 16;
        var ciphertext = combined.AsSpan(0, cipherLen);
        var tag = combined.AsSpan(cipherLen, 16);

        var plaintext = new byte[cipherLen];
        using var gcm = new AesGcm(_masterKey, tagSizeInBytes: 16);
        gcm.Decrypt(secret.Nonce, ciphertext, tag, plaintext);

        return System.Text.Encoding.ASCII.GetString(plaintext);
    }
}
