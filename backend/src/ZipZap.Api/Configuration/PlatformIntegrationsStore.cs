using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;

namespace ZipZap.Api.Configuration;

/// <summary>Zapis integracji platformy (sekrety SZYFROWANE at-rest: captcha, hasło SMTP).</summary>
public sealed record PlatformIntegrations
{
    public string? GoogleClientId { get; init; }         // jawny (nie sekret)
    public string? CaptchaProvider { get; init; }        // "" | "turnstile"
    public string? CaptchaSiteKey { get; init; }         // jawny (renderowany u klienta)
    public string? CaptchaSecretEnc { get; init; }       // zaszyfrowany
    // ---- SMTP (poczta wychodząca) ----
    public string? SmtpHost { get; init; }               // jawny (np. mail-serwer325339.lh.pl)
    public int? SmtpPort { get; init; }                  // np. 465
    public bool SmtpUseSsl { get; init; } = true;        // 465 = SSL bezpośredni
    public string? SmtpUsername { get; init; }           // jawny (login/e-mail konta)
    public string? SmtpPasswordEnc { get; init; }        // zaszyfrowany
    public string? SmtpFromEmail { get; init; }          // adres nadawcy
    public string? SmtpFromName { get; init; }           // nazwa nadawcy (np. Dowózka.pl)
}

/// <summary>Status dla panelu — BEZ sekretów (tylko flaga „ustawiony").</summary>
public sealed record PlatformIntegrationsStatus(
    string? GoogleClientId, string? CaptchaProvider, string? CaptchaSiteKey, bool HasCaptchaSecret,
    string? SmtpHost, int? SmtpPort, bool SmtpUseSsl, string? SmtpUsername,
    string? SmtpFromEmail, string? SmtpFromName, bool HasSmtpPassword);

/// <summary>Dane z panelu. Puste pole sekretu = zachowaj istniejący.</summary>
public sealed record PlatformIntegrationsUpdate(
    string? GoogleClientId, string? CaptchaProvider, string? CaptchaSiteKey, string? CaptchaSecret,
    string? SmtpHost = null, int? SmtpPort = null, bool? SmtpUseSsl = null, string? SmtpUsername = null,
    string? SmtpPassword = null, string? SmtpFromEmail = null, string? SmtpFromName = null);

/// <summary>Konfiguracja captchy po stronie serwera (z odszyfrowanym sekretem) — do weryfikacji.</summary>
public sealed record CaptchaConfig(string? Provider, string? SiteKey, string? Secret)
{
    public bool Enabled => !string.IsNullOrWhiteSpace(Provider) && !string.IsNullOrWhiteSpace(Secret);
}

/// <summary>Konfiguracja SMTP po stronie serwera (z odszyfrowanym hasłem) — do wysyłki.</summary>
public sealed record SmtpConfig(
    string? Host, int Port, bool UseSsl, string? Username, string? Password, string? FromEmail, string? FromName)
{
    public bool Enabled => !string.IsNullOrWhiteSpace(Host) && Port > 0 && !string.IsNullOrWhiteSpace(FromEmail);
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
            !string.IsNullOrEmpty(i.CaptchaSecretEnc),
            i.SmtpHost, i.SmtpPort, i.SmtpUseSsl, i.SmtpUsername, i.SmtpFromEmail, i.SmtpFromName,
            !string.IsNullOrEmpty(i.SmtpPasswordEnc));
    }

    /// <summary>Konfiguracja SMTP z odszyfrowanym hasłem (serwer). Hasło nie opuszcza backendu.</summary>
    public async Task<SmtpConfig> GetSmtpAsync(CancellationToken ct = default)
    {
        var i = await ReadAsync(ct);
        string? pass = null;
        if (!string.IsNullOrEmpty(i.SmtpPasswordEnc))
        {
            try { pass = _dp.Unprotect(i.SmtpPasswordEnc); } catch { pass = null; }
        }
        return new SmtpConfig(i.SmtpHost, i.SmtpPort ?? 0, i.SmtpUseSsl, i.SmtpUsername, pass, i.SmtpFromEmail, i.SmtpFromName);
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
                SmtpHost = Norm(u.SmtpHost),
                SmtpPort = u.SmtpPort ?? existing.SmtpPort,
                SmtpUseSsl = u.SmtpUseSsl ?? existing.SmtpUseSsl,
                SmtpUsername = Norm(u.SmtpUsername),
                SmtpPasswordEnc = string.IsNullOrWhiteSpace(u.SmtpPassword)
                    ? existing.SmtpPasswordEnc
                    : _dp.Protect(u.SmtpPassword.Trim()),
                SmtpFromEmail = Norm(u.SmtpFromEmail),
                SmtpFromName = Norm(u.SmtpFromName),
            };
            await using var s = File.Create(_path);
            await JsonSerializer.SerializeAsync(s, clean, _json, ct);
            return new PlatformIntegrationsStatus(clean.GoogleClientId, clean.CaptchaProvider, clean.CaptchaSiteKey,
                !string.IsNullOrEmpty(clean.CaptchaSecretEnc),
                clean.SmtpHost, clean.SmtpPort, clean.SmtpUseSsl, clean.SmtpUsername,
                clean.SmtpFromEmail, clean.SmtpFromName, !string.IsNullOrEmpty(clean.SmtpPasswordEnc));
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

        // SMTP: gdy podano host — port i adres nadawcy są wymagane.
        var smtpHost = i.SmtpHost?.Trim();
        if (!string.IsNullOrEmpty(smtpHost))
        {
            if ((i.SmtpPort ?? 0) <= 0)
                return "Konfiguracja SMTP wymaga portu (np. 465).";
            var from = i.SmtpFromEmail?.Trim();
            if (string.IsNullOrEmpty(from) || !from.Contains('@'))
                return "Konfiguracja SMTP wymaga poprawnego adresu nadawcy.";
        }
        return null;
    }
}
