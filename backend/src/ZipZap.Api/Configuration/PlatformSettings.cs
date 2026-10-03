namespace ZipZap.Api.Configuration;

/// <summary>
/// Ustawienia platformy edytowalne przez admina (NIE‑sekretne) — dokument konfiguracji w bazie
/// (<see cref="IConfigDocuments"/>).
/// </summary>
public sealed record PlatformSettings
{
    /// <summary>Godziny fal dostaw ("HH:mm"), np. 10:00 / 16:00.</summary>
    public List<string> DeliveryWaves { get; init; } = new() { "10:00", "16:00" };

    /// <summary>Domyślna prowizja (0–1), np. 0.10 = 10%.</summary>
    public decimal DefaultCommissionRate { get; init; } = 0.10m;

    /// <summary>Stała opłata ZipZap za dostawę realizowaną przez nas (Plan A), np. 25 zł.</summary>
    public decimal ZipZapDeliveryFee { get; init; } = 25m;

    public string Currency { get; init; } = "PLN";
    public string? OperatorName { get; init; }
    public string? OperatorContact { get; init; }
}

public sealed class PlatformSettingsStore
{
    public const string Key = "platform-settings";
    private readonly IConfigDocuments _docs;

    public PlatformSettingsStore(IConfigDocuments docs) => _docs = docs;

    public async Task<PlatformSettings> GetAsync(CancellationToken ct = default)
        => await _docs.GetAsync<PlatformSettings>(Key, ct) ?? new PlatformSettings();

    public Task<PlatformSettings> SaveAsync(PlatformSettings settings, CancellationToken ct = default)
    {
        var clean = Sanitize(settings);
        return _docs.UpdateAsync<PlatformSettings>(Key, _ => clean, ct);
    }

    /// <summary>Normalizacja i walidacja wejścia (godziny, zakres prowizji, waluta).</summary>
    private static PlatformSettings Sanitize(PlatformSettings s) => new()
    {
        DeliveryWaves = (s.DeliveryWaves ?? new())
            .Where(w => TimeOnly.TryParse(w, out _))
            .Select(w => TimeOnly.Parse(w).ToString("HH:mm"))
            .Distinct()
            .OrderBy(w => w)
            .ToList(),
        DefaultCommissionRate = Math.Clamp(s.DefaultCommissionRate, 0m, 1m),
        ZipZapDeliveryFee = Math.Max(0m, s.ZipZapDeliveryFee),
        Currency = string.IsNullOrWhiteSpace(s.Currency) ? "PLN" : s.Currency.Trim().ToUpperInvariant(),
        OperatorName = string.IsNullOrWhiteSpace(s.OperatorName) ? null : s.OperatorName!.Trim(),
        OperatorContact = string.IsNullOrWhiteSpace(s.OperatorContact) ? null : s.OperatorContact!.Trim(),
    };
}
