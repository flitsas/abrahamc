namespace Flit.Modules.Auth.Domain;

/// <summary>
/// Encoder/decoder Base32 (RFC 4648, sin padding).
/// Necesario para representar TOTP secrets en formato compatible con Google
/// Authenticator / Authy / 1Password.
///
/// .NET no incluye Base32 built-in; esta implementacion minimalista cubre el
/// caso de uso TOTP. Si en el futuro necesitamos mas casos, considerar el
/// paquete SimpleBase.
/// </summary>
public static class Base32
{
    private const string Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";

    public static string Encode(ReadOnlySpan<byte> data)
    {
        if (data.IsEmpty) return string.Empty;

        var sb = new System.Text.StringBuilder(((data.Length * 8) + 4) / 5);
        int buffer = data[0];
        int bitsLeft = 8;
        int idx = 1;

        while (bitsLeft > 0 || idx < data.Length)
        {
            if (bitsLeft < 5)
            {
                if (idx < data.Length)
                {
                    buffer <<= 8;
                    buffer |= data[idx++] & 0xff;
                    bitsLeft += 8;
                }
                else
                {
                    var pad = 5 - bitsLeft;
                    buffer <<= pad;
                    bitsLeft = 5;
                }
            }
            int index = 0x1f & (buffer >> (bitsLeft - 5));
            bitsLeft -= 5;
            sb.Append(Alphabet[index]);
        }
        return sb.ToString();
    }

    public static byte[] Decode(string base32)
    {
        if (string.IsNullOrWhiteSpace(base32)) return [];

        var input = base32.Trim().TrimEnd('=').ToUpperInvariant();
        var result = new byte[input.Length * 5 / 8];
        int buffer = 0;
        int bitsLeft = 0;
        int idx = 0;

        foreach (var c in input)
        {
            var pos = Alphabet.IndexOf(c);
            if (pos < 0)
                throw new FormatException($"Caracter base32 invalido: '{c}'");
            buffer <<= 5;
            buffer |= pos;
            bitsLeft += 5;
            if (bitsLeft >= 8)
            {
                result[idx++] = (byte)((buffer >> (bitsLeft - 8)) & 0xff);
                bitsLeft -= 8;
            }
        }
        return idx == result.Length ? result : result[..idx];
    }
}
