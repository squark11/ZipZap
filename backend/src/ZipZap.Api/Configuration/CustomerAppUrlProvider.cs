namespace ZipZap.Api.Configuration;

/// <summary>Efektywny adres aplikacji klienta i jego źródło („panel" / „env").</summary>
public sealed record EffectiveCustomerAppUrl(string? Url, string? Source);

/// <summary>
/// Adres aplikacji klienta (web/PWA) — baza stałych linków do sklepów i kodów QR. Najpierw wartość z panelu
/// (Konfiguracja → Integracje platformy, trwale w bazie), potem zmienna <c>PublicApp__CustomerAppUrl</c> jako rezerwa.
/// Brak obu = aplikacja nie jest opublikowana (panel nie generuje kodów QR).
/// </summary>
public sealed class CustomerAppUrlProvider
{
    public const string ConfigKey = "PublicApp:CustomerAppUrl";

    private readonly PlatformIntegrationsStore _store;
    private readonly IConfiguration _config;

    public CustomerAppUrlProvider(PlatformIntegrationsStore store, IConfiguration config)
    {
        _store = store;
        _config = config;
    }

    public async Task<EffectiveCustomerAppUrl> GetAsync(CancellationToken ct = default)
    {
        var fromPanel = (await _store.GetAsync(ct)).CustomerAppUrl;
        if (!string.IsNullOrWhiteSpace(fromPanel)) return new(fromPanel, "panel");
        var fromEnv = CustomerAppUrl.Normalize(_config[ConfigKey]).Value;
        return fromEnv is null ? new(null, null) : new(fromEnv, "env");
    }
}
