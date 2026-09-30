using System.Text;

namespace ZipZap.Modules.Catalog.Domain;

/// <summary>
/// Dzienny licznik wejść na kartę sklepu wg źródła (np. „qr", „qr-kasa", „landing"). Wyłącznie agregat —
/// bez IP, identyfikatora użytkownika, urządzenia ani czasu pojedynczego wejścia (brak danych osobowych).
/// Źródło służy TYLKO do pomiaru: nie daje uprawnień, nie zmienia cen ani ścieżki zamówienia.
/// Dzienne wiersze starsze niż <see cref="StoreEntrySource.DailyRetentionDays"/> dni są sumowane do
/// <see cref="StoreEntryMonthlyStat"/> i usuwane.
/// </summary>
public sealed class StoreEntryStat
{
    public Guid StoreId { get; private set; }
    public DateOnly Day { get; private set; }   // dzień UTC
    public string Source { get; private set; } = default!;
    public int Count { get; private set; }

    private StoreEntryStat() { } // EF
}

/// <summary>Miesięczna suma wejść (po zwinięciu starszych liczników dziennych). Month = 1. dzień miesiąca.</summary>
public sealed class StoreEntryMonthlyStat
{
    public Guid StoreId { get; private set; }
    public DateOnly Month { get; private set; }
    public string Source { get; private set; } = default!;
    public int Count { get; private set; }

    private StoreEntryMonthlyStat() { } // EF
}

/// <summary>
/// Etykieta miejsca kodu QR zarejestrowana przez obsługę sklepu (np. „qr-kasa"). Tylko zarejestrowane etykiety są
/// liczone osobno — anonimowy klient nie może tworzyć nowych źródeł (nieznane trafiają do „qr-other").
/// </summary>
public sealed class StoreQrSource
{
    public Guid StoreId { get; private set; }
    public string Source { get; private set; } = default!;
    public DateTime CreatedAtUtc { get; private set; }
    public Guid? CreatedByUserId { get; private set; }

    private StoreQrSource() { } // EF

    public StoreQrSource(Guid storeId, string source, Guid? createdBy)
    {
        StoreId = storeId;
        Source = source;
        CreatedByUserId = createdBy;
        CreatedAtUtc = DateTime.UtcNow;
    }
}

public static class StoreEntrySource
{
    public const int MaxLength = 40;

    public const string Qr = "qr";              // kod QR bez etykiety miejsca
    public const string Landing = "landing";    // przycisk na stronie dowózka.pl
    public const string LandingQr = "landing-qr";
    public const string Direct = "direct";      // brak źródła
    public const string QrPrefix = "qr-";
    public const string QrOther = "qr-other";   // etykieta QR, której sklep nie zarejestrował
    public const string Other = "other";        // każde inne, nieznane źródło

    /// <summary>Maks. liczba zarejestrowanych etykiet QR na sklep (lokalizację) — kasa, witryna, ulotka… z zapasem.</summary>
    public const int MaxRegisteredQrSources = 20;

    /// <summary>
    /// Ile dni trzymamy liczniki dzienne: 90 dni = kwartał pilotażu i raport S5 (porównanie tygodni, efekt nowego kodu);
    /// starsze są sumowane do liczników miesięcznych (trend długoterminowy bez danych osobowych).
    /// </summary>
    public const int DailyRetentionDays = 90;

    /// <summary>Źródła liczone zawsze, bez rejestracji.</summary>
    public static readonly IReadOnlySet<string> Fixed =
        new HashSet<string>(StringComparer.Ordinal) { Qr, Landing, LandingQr, Direct, QrOther, Other };

    /// <summary>Czy etykietę może zarejestrować obsługa sklepu: „qr-…", nie zarezerwowana, po normalizacji bez zmian.</summary>
    public static bool IsRegistrable(string label)
        => label.StartsWith(QrPrefix, StringComparison.Ordinal) && label.Length > QrPrefix.Length
           && label != QrOther && Normalize(label) == label;

    /// <summary>
    /// Normalizacja etykiety źródła z adresu: małe litery, cyfry i „-" (maks. 40 znaków); inne znaki usuwane,
    /// pusto → „direct". Dzięki temu parametr z adresu nie wprowadzi do bazy dowolnego tekstu.
    /// </summary>
    public static string Normalize(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return Direct;
        var sb = new StringBuilder(MaxLength);
        foreach (var ch in raw.Trim().ToLowerInvariant())
        {
            if (sb.Length == MaxLength) break;
            if (ch is >= 'a' and <= 'z' or >= '0' and <= '9') sb.Append(ch);
            else if ((ch is '-' or '_' or ' ' or '.') && sb.Length > 0 && sb[^1] != '-') sb.Append('-');
        }
        var s = sb.ToString().Trim('-');
        return s.Length == 0 ? Direct : s;
    }
}
