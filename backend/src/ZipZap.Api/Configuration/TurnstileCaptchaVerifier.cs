using System.Text.Json;
using Microsoft.Extensions.Logging;
using ZipZap.BuildingBlocks.Security;

namespace ZipZap.Api.Configuration;

/// <summary>
/// Weryfikator captchy Cloudflare Turnstile. Czyta konfigurację (provider + sekret) z magazynu
/// panelu; gdy captcha wyłączona — przepuszcza (true). W innym wypadku woła siteverify Cloudflare.
/// </summary>
public sealed class TurnstileCaptchaVerifier : ICaptchaVerifier
{
    private const string VerifyUrl = "https://challenges.cloudflare.com/turnstile/v0/siteverify";

    private readonly HttpClient _http;
    private readonly PlatformIntegrationsStore _store;
    private readonly ILogger<TurnstileCaptchaVerifier> _logger;

    public TurnstileCaptchaVerifier(HttpClient http, PlatformIntegrationsStore store, ILogger<TurnstileCaptchaVerifier> logger)
    {
        _http = http;
        _store = store;
        _logger = logger;
    }

    public async Task<bool> VerifyAsync(string? token, CancellationToken ct = default)
    {
        var cfg = await _store.GetCaptchaAsync(ct);
        if (!cfg.Enabled) return true;                 // captcha nieskonfigurowana → brak blokady
        if (cfg.Provider != "turnstile") return true;   // nieznany provider → nie blokuj (fail-open na konfig)
        if (string.IsNullOrWhiteSpace(token)) return false;

        try
        {
            using var form = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["secret"] = cfg.Secret!,
                ["response"] = token,
            });
            var resp = await _http.PostAsync(VerifyUrl, form, ct);
            if (!resp.IsSuccessStatusCode) return false;

            await using var stream = await resp.Content.ReadAsStreamAsync(ct);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
            return doc.RootElement.TryGetProperty("success", out var s) && s.GetBoolean();
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Weryfikacja captchy nie powiodła się: {Message}", ex.Message);
            return false;
        }
    }
}
