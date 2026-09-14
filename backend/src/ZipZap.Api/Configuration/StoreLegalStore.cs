using System.Text.Json;

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
/// czy akceptacja jest wymagana. Store pilotażowy w pliku JSON (wymienialny na DB).
/// </summary>
public sealed class StoreLegalStore
{
    private readonly string _path;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private static readonly JsonSerializerOptions _json = new() { WriteIndented = true };

    public StoreLegalStore(IHostEnvironment env)
    {
        var dir = Path.Combine(env.ContentRootPath, "App_Data");
        Directory.CreateDirectory(dir);
        _path = Path.Combine(dir, "store-legal.json");
    }

    private async Task<Dictionary<string, StoreLegal>> ReadAllAsync(CancellationToken ct)
    {
        if (!File.Exists(_path)) return new();
        try
        {
            await using var s = File.OpenRead(_path);
            return await JsonSerializer.DeserializeAsync<Dictionary<string, StoreLegal>>(s, _json, ct) ?? new();
        }
        catch { return new(); }
    }

    public async Task<StoreLegal> GetAsync(Guid storeId, CancellationToken ct = default)
    {
        var all = await ReadAllAsync(ct);
        return all.TryGetValue(storeId.ToString(), out var l) ? l : new StoreLegal();
    }

    public async Task<StoreLegal> SaveAsync(Guid storeId, StoreLegal update, CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct);
        try
        {
            var all = await ReadAllAsync(ct);
            var clean = new StoreLegal
            {
                TermsUrl = Normalize(update.TermsUrl),
                PrivacyUrl = Normalize(update.PrivacyUrl),
                GdprUrl = Normalize(update.GdprUrl),
                RequiresAcceptance = update.RequiresAcceptance,
            };
            all[storeId.ToString()] = clean;

            await using var s = File.Create(_path);
            await JsonSerializer.SerializeAsync(s, all, _json, ct);
            return clean;
        }
        finally { _lock.Release(); }
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
