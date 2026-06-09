using System.Security.Cryptography;

namespace Flit.Modules.Auth.Domain;

/// <summary>
/// Implementacion TOTP (RFC 6238) compatible con Google Authenticator,
/// Authy, 1Password, Microsoft Authenticator.
///
/// Algoritmo:
///   1. counter = unix_timestamp / 30s
///   2. hmac = HMAC-SHA1(counter_big_endian, secret_bytes)
///   3. offset = hmac[19] &amp; 0x0f
///   4. truncated = (hmac[offset..offset+4] &amp; 0x7fffffff) % 10^digits
///   5. format con padding cero a la izquierda
///
/// Periodo estandar: 30 segundos. Digitos estandar: 6.
/// Window de tolerancia: ±1 step (es decir, ±30s) por defecto para tolerar
/// clock skew razonable sin debilitar seguridad.
/// </summary>
public sealed class TotpService
{
    private readonly int _period;
    private readonly int _digits;
    private readonly int _window;

    public TotpService(int period = 30, int digits = 6, int window = 1)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(period);
        if (digits is < 6 or > 8) throw new ArgumentOutOfRangeException(nameof(digits));
        ArgumentOutOfRangeException.ThrowIfNegative(window);
        _period = period;
        _digits = digits;
        _window = window;
    }

    /// <summary>
    /// Genera un secret aleatorio (32 bytes / 256 bits) en base32, suficiente
    /// para HMAC-SHA1.
    /// </summary>
    public static string GenerateSecret(int byteLength = 32)
    {
        var bytes = RandomNumberGenerator.GetBytes(byteLength);
        return Base32.Encode(bytes);
    }

    /// <summary>
    /// Construye la URI otpauth:// que el usuario escanea con Google Authenticator.
    /// Formato: otpauth://totp/{issuer}:{accountLabel}?secret=...&amp;issuer={issuer}&amp;algorithm=SHA1&amp;digits=6&amp;period=30
    /// </summary>
    public string BuildOtpAuthUri(string secretBase32, string issuer, string accountLabel)
    {
        if (string.IsNullOrWhiteSpace(secretBase32))
            throw new ArgumentException("Secret requerido", nameof(secretBase32));
        if (string.IsNullOrWhiteSpace(issuer))
            throw new ArgumentException("Issuer requerido", nameof(issuer));
        if (string.IsNullOrWhiteSpace(accountLabel))
            throw new ArgumentException("AccountLabel requerido", nameof(accountLabel));

        var issuerEnc = Uri.EscapeDataString(issuer);
        var labelEnc = Uri.EscapeDataString(accountLabel);
        return $"otpauth://totp/{issuerEnc}:{labelEnc}?secret={secretBase32}" +
               $"&issuer={issuerEnc}&algorithm=SHA1&digits={_digits}&period={_period}";
    }

    /// <summary>
    /// Calcula el codigo TOTP esperado en el momento now para el secret dado.
    /// </summary>
    public string Generate(string secretBase32, DateTimeOffset now)
    {
        var counter = ToCounter(now);
        return Compute(Base32.Decode(secretBase32), counter);
    }

    /// <summary>
    /// Valida un codigo dado contra el secret con window de tolerancia.
    /// Retorna true si el codigo coincide en step actual o en window steps
    /// anteriores/siguientes.
    /// </summary>
    public bool Verify(string secretBase32, string code, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(code)) return false;
        code = code.Trim();
        if (code.Length != _digits) return false;

        var secretBytes = Base32.Decode(secretBase32);
        var currentCounter = ToCounter(now);

        for (int offset = -_window; offset <= _window; offset++)
        {
            var counter = currentCounter + offset;
            var expected = Compute(secretBytes, counter);
            if (FixedTimeEquals(expected, code))
                return true;
        }
        return false;
    }

    // ─── Helpers ───────────────────────────────────────────────────────

    private long ToCounter(DateTimeOffset now)
        => now.ToUnixTimeSeconds() / _period;

    private string Compute(byte[] secretBytes, long counter)
    {
        Span<byte> counterBytes = stackalloc byte[8];
        for (int i = 7; i >= 0; i--)
        {
            counterBytes[i] = (byte)(counter & 0xff);
            counter >>= 8;
        }

        using var hmac = new HMACSHA1(secretBytes);
        Span<byte> hash = stackalloc byte[20];
        hmac.TryComputeHash(counterBytes, hash, out _);

        int offset = hash[^1] & 0x0f;
        int binCode = ((hash[offset] & 0x7f) << 24)
                     | ((hash[offset + 1] & 0xff) << 16)
                     | ((hash[offset + 2] & 0xff) << 8)
                     | (hash[offset + 3] & 0xff);

        var modulo = (int)Math.Pow(10, _digits);
        var otp = binCode % modulo;
        return otp.ToString(new string('0', _digits), System.Globalization.CultureInfo.InvariantCulture);
    }

    /// <summary>Comparacion constante en tiempo para evitar timing attacks.</summary>
    private static bool FixedTimeEquals(string a, string b)
    {
        if (a.Length != b.Length) return false;
        int diff = 0;
        for (int i = 0; i < a.Length; i++)
            diff |= a[i] ^ b[i];
        return diff == 0;
    }
}
