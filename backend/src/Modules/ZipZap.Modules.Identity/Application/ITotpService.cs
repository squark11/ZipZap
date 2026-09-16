namespace ZipZap.Modules.Identity.Application;

/// <summary>
/// TOTP (RFC 6238) — hasła jednorazowe oparte na czasie, zgodne z aplikacjami
/// authenticator (Google Authenticator, Authy, Microsoft Authenticator, ...).
/// </summary>
public interface ITotpService
{
    /// <summary>Nowy losowy sekret zakodowany w Base32 (do zapisania i pokazania w QR).</summary>
    string GenerateSecret();

    /// <summary>URI otpauth:// do wygenerowania kodu QR w panelu.</summary>
    string BuildOtpAuthUri(string secretBase32, string accountEmail, string issuer);

    /// <summary>
    /// Weryfikuje 6-cyfrowy kod względem sekretu, tolerując przesunięcie zegara
    /// o ±<paramref name="window"/> kroków (domyślnie ±1 krok = ±30 s).
    /// </summary>
    bool Verify(string secretBase32, string code, int window = 1);
}
