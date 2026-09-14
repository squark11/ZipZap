using System.Text.Json;

namespace ZipZap.Api.Configuration;

/// <summary>
/// Integracje platformy edytowalne w panelu admina (zamiast env). Na razie: Google OAuth Client ID
/// (identyfikator jawny — nie sekret). Kolejne pozycje (np. e-mail/SMTP) dojdą tu; sekrety wymagają szyfrowania.
/// </summary>
public sealed record PlatformIntegrations
{
    public string? GoogleClientId { get; init; }
}

/// <summary>
/// Magazyn integracji platformy (App_Data JSON, wymienialny na DB). Wartości jawne.
/// </summary>
public sealed class PlatformIntegrationsStore
{
    private readonly string _path;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private static readonly JsonSerializerOptions _json = new() { WriteIndented = true };

    public PlatformIntegrationsStore(IHostEnvironment env)
    {
        var dir = Path.Combine(env.ContentRootPath, "App_Data");
        Directory.CreateDirectory(dir);
        _path = Path.Combine(dir, "platform-integrations.json");
    }

    public async Task<PlatformIntegrations> GetAsync(CancellationToken ct = default)
    {
        if (!File.Exists(_path)) return new();
        try
        {
            await using var s = File.OpenRead(_path);
            return await JsonSerializer.DeserializeAsync<PlatformIntegrations>(s, _json, ct) ?? new();
        }
        catch { return new(); }
    }

    public async Task<PlatformIntegrations> SaveAsync(PlatformIntegrations update, CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct);
        try
        {
            var clean = new PlatformIntegrations
            {
                GoogleClientId = string.IsNullOrWhiteSpace(update.GoogleClientId) ? null : update.GoogleClientId.Trim(),
            };
            await using var s = File.Create(_path);
            await JsonSerializer.SerializeAsync(s, clean, _json, ct);
            return clean;
        }
        finally { _lock.Release(); }
    }

    /// <summary>Zwraca komunikat błędu lub null. Google Client ID (gdy podany) musi kończyć się `.apps.googleusercontent.com`.</summary>
    public static string? Validate(PlatformIntegrations i)
    {
        var gid = i.GoogleClientId?.Trim();
        if (!string.IsNullOrEmpty(gid) && !gid.EndsWith(".apps.googleusercontent.com", StringComparison.OrdinalIgnoreCase))
            return "Google Client ID powinien kończyć się na '.apps.googleusercontent.com' (identyfikator klienta typu Web).";
        return null;
    }
}
