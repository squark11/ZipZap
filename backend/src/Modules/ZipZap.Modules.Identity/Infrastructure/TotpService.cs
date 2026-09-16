using System.Security.Cryptography;
using System.Text;
using ZipZap.Modules.Identity.Application;

namespace ZipZap.Modules.Identity.Infrastructure;

/// <summary>
/// Implementacja TOTP (RFC 6238) na HMAC-SHA1, krok 30 s, 6 cyfr — bez zależności
/// zewnętrznych. Sekret przechowywany w Base32 (standard authenticatorów).
/// </summary>
public sealed class TotpService : ITotpService
{
    private const int StepSeconds = 30;
    private const int Digits = 6;
    private const string Base32Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";

    public string GenerateSecret()
    {
        var bytes = RandomNumberGenerator.GetBytes(20); // 160 bitów
        return Base32Encode(bytes);
    }

    public string BuildOtpAuthUri(string secretBase32, string accountEmail, string issuer)
    {
        var iss = Uri.EscapeDataString(issuer);
        var acc = Uri.EscapeDataString(accountEmail);
        return $"otpauth://totp/{iss}:{acc}?secret={secretBase32}&issuer={iss}&algorithm=SHA1&digits={Digits}&period={StepSeconds}";
    }

    public bool Verify(string secretBase32, string code, int window = 1)
    {
        if (string.IsNullOrWhiteSpace(code)) return false;
        code = code.Trim().Replace(" ", "");
        if (code.Length != Digits || !code.All(char.IsDigit)) return false;

        byte[] key;
        try { key = Base32Decode(secretBase32); }
        catch { return false; }

        var counter = DateTimeOffset.UtcNow.ToUnixTimeSeconds() / StepSeconds;
        for (var offset = -window; offset <= window; offset++)
        {
            var candidate = Compute(key, counter + offset);
            // Porównanie w stałym czasie, by nie wyciekać informacji timingiem.
            if (CryptographicOperations.FixedTimeEquals(
                    Encoding.ASCII.GetBytes(candidate), Encoding.ASCII.GetBytes(code)))
                return true;
        }
        return false;
    }

    /// <summary>Kod dla konkretnego licznika kroków — do testów (wektory RFC 6238).</summary>
    internal string GenerateCode(string secretBase32, long counter)
        => Compute(Base32Decode(secretBase32), counter);

    private static string Compute(byte[] key, long counter)
    {
        var counterBytes = BitConverter.GetBytes(counter);
        if (BitConverter.IsLittleEndian) Array.Reverse(counterBytes);

        using var hmac = new HMACSHA1(key);
        var hash = hmac.ComputeHash(counterBytes);

        var offset = hash[^1] & 0x0F;
        var binary = ((hash[offset] & 0x7F) << 24)
                     | ((hash[offset + 1] & 0xFF) << 16)
                     | ((hash[offset + 2] & 0xFF) << 8)
                     | (hash[offset + 3] & 0xFF);
        var otp = binary % (int)Math.Pow(10, Digits);
        return otp.ToString().PadLeft(Digits, '0');
    }

    private static string Base32Encode(byte[] data)
    {
        var sb = new StringBuilder((data.Length + 4) / 5 * 8);
        int buffer = 0, bitsLeft = 0;
        foreach (var b in data)
        {
            buffer = (buffer << 8) | b;
            bitsLeft += 8;
            while (bitsLeft >= 5)
            {
                bitsLeft -= 5;
                sb.Append(Base32Alphabet[(buffer >> bitsLeft) & 0x1F]);
            }
        }
        if (bitsLeft > 0)
            sb.Append(Base32Alphabet[(buffer << (5 - bitsLeft)) & 0x1F]);
        return sb.ToString();
    }

    private static byte[] Base32Decode(string input)
    {
        input = input.Trim().TrimEnd('=').ToUpperInvariant().Replace(" ", "");
        var bytes = new List<byte>(input.Length * 5 / 8);
        int buffer = 0, bitsLeft = 0;
        foreach (var c in input)
        {
            var val = Base32Alphabet.IndexOf(c);
            if (val < 0) throw new FormatException("Nieprawidłowy znak Base32.");
            buffer = (buffer << 5) | val;
            bitsLeft += 5;
            if (bitsLeft >= 8)
            {
                bitsLeft -= 8;
                bytes.Add((byte)((buffer >> bitsLeft) & 0xFF));
            }
        }
        return bytes.ToArray();
    }
}
