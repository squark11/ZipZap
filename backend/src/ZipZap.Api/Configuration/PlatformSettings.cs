using System.Text.Json;

namespace ZipZap.Api.Configuration;

/// <summary>
/// Ustawienia platformy edytowalne przez admina (NIE‑sekretne). Na pilotaż
/// trzymane w pliku JSON (content root/App_Data); interfejs pozwala później
/// podmienić na store w bazie bez zmian w API/panelu.
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
    private readonly string _path;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private static readonly JsonSerializerOptions _json = new() { WriteIndented = true };

    public PlatformSettingsStore(IHostEnvironment env)
    {
        var dir = Path.Combine(env.ContentRootPath, "App_Data");
        Directory.CreateDirectory(dir);
        _path = Path.Combine(dir, "platform-settings.json");
    }

    public async Task<PlatformSettings> GetAsync(CancellationToken ct = default)
    {
        if (!File.Exists(_path)) return new PlatformSettings();
        try
        {
            await using var s = File.OpenRead(_path);
            return await JsonSerializer.DeserializeAsync<PlatformSettings>(s, _json, ct) ?? new PlatformSettings();
        }
        catch
        {
            return new PlatformSettings();
        }
    }

    public async Task<PlatformSettings> SaveAsync(PlatformSettings settings, CancellationToken ct = default)
    {
        var clean = Sanitize(settings);
        await _lock.WaitAsync(ct);
        try
        {
            await using var s = File.Create(_path);
            await JsonSerializer.SerializeAsync(s, clean, _json, ct);
        }
        finally
        {
            _lock.Release();
        }
        return clean;
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
