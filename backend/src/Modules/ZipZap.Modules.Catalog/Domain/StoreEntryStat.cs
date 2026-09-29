using System.Text;

namespace ZipZap.Modules.Catalog.Domain;

/// <summary>
/// Dzienny licznik wejść na kartę sklepu wg źródła (np. „qr", „qr-kasa", „landing"). Wyłącznie agregat —
/// bez IP, identyfikatora użytkownika, urządzenia ani czasu pojedynczego wejścia (brak danych osobowych).
/// Źródło służy TYLKO do pomiaru: nie daje uprawnień, nie zmienia cen ani ścieżki zamówienia.
/// </summary>
public sealed class StoreEntryStat
{
    public Guid StoreId { get; private set; }
    public DateOnly Day { get; private set; }   // dzień UTC
    public string Source { get; private set; } = default!;
    public int Count { get; private set; }

    private StoreEntryStat() { } // EF
}

public static class StoreEntrySource
{
    public const int MaxLength = 40;
    public const string Direct = "direct";

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
