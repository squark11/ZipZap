namespace ZipZap.Modules.Identity.Application;

public sealed class IdentityOptions
{
    public const string SectionName = "Identity";

    /// <summary>
    /// Bazowy URL panelu (linki w e-mailach): <c>{PublicUrl}/reset-password</c> to wspólna strona resetu hasła dla
    /// WSZYSTKICH kont (sklep, kierowca, klient aplikacji). Publicznie musi być HTTPS.
    /// </summary>
    public string PublicUrl { get; set; } = "http://localhost:4200";
    public int EmailVerificationHours { get; set; } = 24;
    public int PasswordResetHours { get; set; } = 1;

    /// <summary>Ile linków resetu może dostać jedno konto w ciągu godziny (ponad limit — cicho bez wysyłki).</summary>
    public int PasswordResetMaxPerHour { get; set; } = 3;

    /// <summary>
    /// Problem z <see cref="PublicUrl"/> dla środowiska publicznego (null = OK): brak HTTPS, localhost albo zły format.
    /// Link w e-mailu musi prowadzić na istniejącą stronę resetu po HTTPS.
    /// </summary>
    public static string? PublicUrlProblem(string? publicUrl, bool publicEnvironment)
    {
        if (!Uri.TryCreate(publicUrl, UriKind.Absolute, out var uri) || (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp))
            return "Identity:PublicUrl nie jest poprawnym adresem http(s).";
        if (!string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment))
            return "Identity:PublicUrl nie może zawierać „?\" ani „#\".";
        if (!publicEnvironment) return null;
        if (uri.IsLoopback || uri.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase))
            return "Identity:PublicUrl wskazuje localhost — linki w e-mailach nie zadziałają u użytkowników.";
        if (uri.Scheme != Uri.UriSchemeHttps)
            return "Identity:PublicUrl musi używać HTTPS w środowisku publicznym.";
        return null;
    }
}
