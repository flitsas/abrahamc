using System.Security.Cryptography;
using System.Text;
using Konscious.Security.Cryptography;
using Flit.Modules.Identity.Ports;

namespace Flit.Modules.Identity.Adapters;

/// <summary>
/// Argon2id hasher (ADR-0006 §"Politica de contrasenas"):
/// 32 MB memoria, 4 iteraciones, 1 paralelo. OWASP 2023+.
///
/// Formato del hash: $argon2id$v=19$m=32768,t=4,p=1$&lt;saltB64&gt;$&lt;hashB64&gt;
/// (no usamos modular crypto $ format de la libreria — guardamos JSON-friendly).
/// </summary>
public sealed class Argon2idPasswordHasher : IPasswordHasher
{
    private const int SaltSize = 16;
    private const int HashSize = 32;
    private const int MemorySizeKb = 32 * 1024;
    private const int Iterations = 4;
    private const int DegreeOfParallelism = 1;
    private const string Algorithm = "argon2id";

    public string Hash(string password)
    {
        ArgumentException.ThrowIfNullOrEmpty(password);

        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var hash = ComputeHash(password, salt);

        return $"${Algorithm}$v=19$m={MemorySizeKb},t={Iterations},p={DegreeOfParallelism}$" +
               $"{Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}";
    }

    public bool Verify(string password, string hash)
    {
        if (string.IsNullOrEmpty(password) || string.IsNullOrEmpty(hash))
            return false;

        var parts = hash.Split('$');
        // Expected: ["", "argon2id", "v=19", "m=...,t=...,p=...", "<saltB64>", "<hashB64>"]
        if (parts.Length != 6 || parts[1] != Algorithm)
            return false;

        try
        {
            var salt = Convert.FromBase64String(parts[4]);
            var expected = Convert.FromBase64String(parts[5]);
            var actual = ComputeHash(password, salt);

            return CryptographicOperations.FixedTimeEquals(actual, expected);
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static byte[] ComputeHash(string password, byte[] salt)
    {
        using var argon = new Argon2id(Encoding.UTF8.GetBytes(password))
        {
            Salt = salt,
            DegreeOfParallelism = DegreeOfParallelism,
            Iterations = Iterations,
            MemorySize = MemorySizeKb,
        };
        return argon.GetBytes(HashSize);
    }
}
