using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;

namespace ZipZap.Api.Configuration;

/// <summary>Zapis integracji platformy (sekret captchy SZYFROWANY at-rest).</summary>
public sealed record PlatformIntegrations
{
    public string? GoogleClientId { get; init; }         // jawny (nie sekret)
    public string? CaptchaProvider { get; init; }        // "" | "turnstile"
    public string? CaptchaSiteKey { get; init; }         // jawny (renderowany u klienta)
    public string? CaptchaSecretEnc { get; init; }       // zaszyfrowany
}

/// <summary>Status dla panelu — BEZ sekretów (tylko flaga „ustawiony").</summary>
public sealed record PlatformIntegrationsStatus(
    string? GoogleClientId, string? CaptchaProvider, string? CaptchaSiteKey, bool HasCaptchaSecret);

/// <summary>Dane z panelu. Puste pole sekretu = zachowaj istniejący.</summary>
public sealed record PlatformIntegrationsUpdate(
    string? GoogleClientId, string? CaptchaProvider, string? CaptchaSiteKey, string? CaptchaSecret);

/// <summary>Konfiguracja captchy po stronie serwera (z odszyfrowanym sekretem) — do weryfikacji.</summary>
public sealed record CaptchaConfig(string? Provider, string? SiteKey, string? Secret)
{
    public bool Enabled => !string.IsNullOrWhiteSpace(Provider) && !string.IsNullOrWhiteSpace(Secret);
}

/// <summary>
/// Magazyn integracji platformy (App_Data JSON, wymienialny na DB). Google Client ID jawny;
/// sekret captchy szyfrowany (IDataProtector), nigdy nie zwracany do klienta (write-only).
/// </summary>
public sealed class PlatformIntegrationsStore
{
    private readonly string _path;
    private readonly IDataProtector _dp;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private static readonly JsonSerializerOptions _json = new() { WriteIndented = true };

    public PlatformIntegrationsStore(IHostEnvironment env, IDataProtectionProvider dpp)
    {
        var dir = Path.Combine(env.ContentRootPath, "App_Data");
        Directory.CreateDirectory(dir);
        _path = Path.Combine(dir, "platform-integrations.json");
        _dp = dpp.CreateProtector("ZipZap.PlatformIntegrations.v1");
    }

    private async Task<PlatformIntegrations> ReadAsync(CancellationToken ct)
    {
        if (!File.Exists(_path)) return new();
        try
        {
            await using var s = File.OpenRead(_path);
            return await JsonSerializer.DeserializeAsync<PlatformIntegrations>(s, _json, ct) ?? new();
        }
        catch { return new(); }
    }

    /// <summary>Surowy zapis (dla providera Google Client ID). Nie zwracać sekretu do klienta.</summary>
    public Task<PlatformIntegrations> GetAsync(CancellationToken ct = default) => ReadAsync(ct);

    public async Task<PlatformIntegrationsStatus> GetStatusAsync(CancellationToken ct = default)
    {
        var i = await ReadAsync(ct);
        return new PlatformIntegrationsStatus(i.GoogleClientId, i.CaptchaProvider, i.CaptchaSiteKey,
            !string.IsNullOrEmpty(i.CaptchaSecretEnc));
    }

    /// <summary>Konfiguracja captchy z odszyfrowanym sekretem (serwer). Sekret nie opuszcza backendu.</summary>
    public async Task<CaptchaConfig> GetCaptchaAsync(CancellationToken ct = default)
    {
        var i = await ReadAsync(ct);
        string? secret = null;
        if (!string.IsNullOrEmpty(i.CaptchaSecretEnc))
        {
            try { secret = _dp.Unprotect(i.CaptchaSecretEnc); } catch { secret = null; }
        }
        return new CaptchaConfig(i.CaptchaProvider, i.CaptchaSiteKey, secret);
    }

    public async Task<PlatformIntegrationsStatus> SaveAsync(PlatformIntegrationsUpdate u, CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct);
        try
        {
            var existing = await ReadAsync(ct);
            var clean = new PlatformIntegrations
            {
                GoogleClientId = Norm(u.GoogleClientId),
                CaptchaProvider = string.IsNullOrWhiteSpace(u.CaptchaProvider) ? null : u.CaptchaProvider.Trim().ToLowerInvariant(),
                CaptchaSiteKey = Norm(u.CaptchaSiteKey),
                CaptchaSecretEnc = string.IsNullOrWhiteSpace(u.CaptchaSecret)
                    ? existing.CaptchaSecretEnc
                    : _dp.Protect(u.CaptchaSecret.Trim()),
            };
            await using var s = File.Create(_path);
            await JsonSerializer.SerializeAsync(s, clean, _json, ct);
            return new PlatformIntegrationsStatus(clean.GoogleClientId, clean.CaptchaProvider, clean.CaptchaSiteKey,
                !string.IsNullOrEmpty(clean.CaptchaSecretEnc));
        }
        finally { _lock.Release(); }
    }

    private static string? Norm(string? v) => string.IsNullOrWhiteSpace(v) ? null : v.Trim();

    /// <summary>Zwraca komunikat błędu lub null. Waliduje Google Client ID i spójność konfiguracji captchy.</summary>
    public static string? Validate(PlatformIntegrationsUpdate i)
    {
        var gid = i.GoogleClientId?.Trim();
        if (!string.IsNullOrEmpty(gid) && !gid.EndsWith(".apps.googleusercontent.com", StringComparison.OrdinalIgnoreCase))
            return "Google Client ID powinien kończyć się na '.apps.googleusercontent.com' (identyfikator klienta typu Web).";

        var provider = i.CaptchaProvider?.Trim().ToLowerInvariant();
        if (!string.IsNullOrEmpty(provider))
        {
            if (provider != "turnstile")
                return "Obsługiwany dostawca captchy: 'turnstile'.";
            if (string.IsNullOrWhiteSpace(i.CaptchaSiteKey))
                return "Włączenie captchy wymaga klucza witryny (site key).";
        }
        return null;
    }
}
