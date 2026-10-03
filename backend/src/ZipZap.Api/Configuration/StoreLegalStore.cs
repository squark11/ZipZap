namespace ZipZap.Api.Configuration;

/// <summary>
/// Dokumenty prawne sklepu (adresy URL) + wymóg akceptacji przez klienta przed zakupem.
/// Dane jawne (linki publiczne) — używane i przez panel (zapis), i przez checkout (odczyt).
/// </summary>
public sealed record StoreLegal
{
    public string? TermsUrl { get; init; }        // regulamin
    public string? PrivacyUrl { get; init; }      // polityka prywatności
    public string? GdprUrl { get; init; }         // RODO / obowiązek informacyjny
    public bool RequiresAcceptance { get; init; } // klient musi zaakceptować przed zakupem
}

/// <summary>
/// Per-store magazyn dokumentów prawnych. Każdy sklep zamieszcza SWOJE linki i decyduje,
/// czy akceptacja jest wymagana. Jeden dokument konfiguracji na sklep (<see cref="IConfigDocuments"/>).
/// </summary>
public sealed class StoreLegalStore
{
    public const string KeyPrefix = "store-legal:";
    private readonly IConfigDocuments _docs;

    public StoreLegalStore(IConfigDocuments docs) => _docs = docs;

    public async Task<StoreLegal> GetAsync(Guid storeId, CancellationToken ct = default)
        => await _docs.GetAsync<StoreLegal>(KeyPrefix + storeId, ct) ?? new StoreLegal();

    public Task<StoreLegal> SaveAsync(Guid storeId, StoreLegal update, CancellationToken ct = default)
    {
        var clean = new StoreLegal
        {
            TermsUrl = Normalize(update.TermsUrl),
            PrivacyUrl = Normalize(update.PrivacyUrl),
            GdprUrl = Normalize(update.GdprUrl),
            RequiresAcceptance = update.RequiresAcceptance,
        };
        return _docs.UpdateAsync<StoreLegal>(KeyPrefix + storeId, _ => clean, ct);
    }

    private static string? Normalize(string? url)
        => string.IsNullOrWhiteSpace(url) ? null : url.Trim();

    /// <summary>Zwraca komunikat błędu lub null gdy dane poprawne. URL-e muszą być http/https;
    /// przy wymogu akceptacji regulamin i polityka prywatności są obowiązkowe.</summary>
    public static string? Validate(StoreLegal l)
    {
        foreach (var (url, name) in new[] { (l.TermsUrl, "Regulamin"), (l.PrivacyUrl, "Polityka prywatności"), (l.GdprUrl, "RODO") })
        {
            if (string.IsNullOrWhiteSpace(url)) continue;
            if (!Uri.TryCreate(url.Trim(), UriKind.Absolute, out var u) || (u.Scheme != Uri.UriSchemeHttp && u.Scheme != Uri.UriSchemeHttps))
                return $"{name}: podaj poprawny adres URL (http/https).";
        }
        if (l.RequiresAcceptance && (string.IsNullOrWhiteSpace(l.TermsUrl) || string.IsNullOrWhiteSpace(l.PrivacyUrl)))
            return "Aby wymagać akceptacji, podaj co najmniej regulamin i politykę prywatności.";
        return null;
    }
}
