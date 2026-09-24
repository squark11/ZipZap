using System.Net.Http.Headers;
using System.Net.Http.Json;
using ZipZap.BuildingBlocks.Logging;
using ZipZap.Modules.Identity.Application;

namespace ZipZap.Api.Configuration;

/// <summary>
/// Wysyłka e-maili przez HTTP API dostawcy (Resend / Brevo) — port 443.
/// Powód: hosting (Render) BLOKUJE wychodzące porty SMTP (25/465/587), więc MailKit
/// się zawiesza. HTTP API omija ten problem. Gdy klucz API nie jest ustawiony, delegujemy
/// do <see cref="SmtpEmailSender"/> (działa lokalnie i na hostingach bez blokady portów).
///
/// Konfiguracja (env, sekret trzymany poza repo — np. w zmiennych usługi Render):
///   Email:Http:Provider   = resend | brevo   (domyślnie resend)
///   Email:Http:ApiKey     = klucz API dostawcy
///   Email:Http:FromEmail  = adres nadawcy (dla Resend z domeny zweryfikowanej w dostawcy)
///   Email:Http:FromName   = nazwa nadawcy (domyślnie Dowózka.pl)
/// </summary>
public sealed class HttpEmailSender : IEmailSender
{
    private readonly HttpClient _http;
    private readonly IConfiguration _cfg;
    private readonly SmtpEmailSender _smtpFallback;
    private readonly PlatformIntegrationsStore _store;
    private readonly ILogger<HttpEmailSender> _logger;

    public HttpEmailSender(
        HttpClient http, IConfiguration cfg, SmtpEmailSender smtpFallback,
        PlatformIntegrationsStore store, ILogger<HttpEmailSender> logger)
    {
        _http = http;
        _cfg = cfg;
        _smtpFallback = smtpFallback;
        _store = store;
        _logger = logger;
    }

    public async Task SendAsync(EmailMessage message, CancellationToken ct = default)
    {
        var apiKey = _cfg["Email:Http:ApiKey"];
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            // Brak dostawcy HTTP → tradycyjny SMTP (lokalnie / hosting bez blokady portów) lub mock.
            await _smtpFallback.SendAsync(message, ct);
            return;
        }

        var provider = (_cfg["Email:Http:Provider"] ?? "resend").Trim().ToLowerInvariant();
        var fromEmail = _cfg["Email:Http:FromEmail"]
            ?? _cfg["Email:Smtp:FromEmail"]
            ?? (await _store.GetSmtpAsync(ct)).FromEmail;
        var fromName = _cfg["Email:Http:FromName"] ?? _cfg["Email:Smtp:FromName"] ?? "Dowózka.pl";

        if (string.IsNullOrWhiteSpace(fromEmail))
            throw new InvalidOperationException("Brak adresu nadawcy — ustaw Email:Http:FromEmail.");

        var req = provider switch
        {
            "brevo" => BuildBrevo(apiKey, fromEmail!, fromName!, message),
            "resend" or "" => BuildResend(apiKey, fromEmail!, fromName!, message),
            _ => throw new InvalidOperationException($"Nieznany dostawca e-mail HTTP '{provider}' (obsługiwane: resend, brevo)."),
        };

        using var resp = await _http.SendAsync(req, ct);
        var body = await resp.Content.ReadAsStringAsync(ct);
        if (!resp.IsSuccessStatusCode)
        {
            _logger.LogWarning("[EMAIL:http] {Provider} {Status}: {Body}", provider, (int)resp.StatusCode, Trunc(body));
            throw new InvalidOperationException($"Dostawca e-mail ({provider}) zwrócił {(int)resp.StatusCode}: {Trunc(body)}");
        }
        _logger.LogInformation("[EMAIL:http] wysłano ({Provider}) to={To} subject={Subject}", provider, EmailLog.Mask(message.To), message.Subject);
    }

    private static HttpRequestMessage BuildResend(string apiKey, string from, string fromName, EmailMessage m)
    {
        var payload = new { from = $"{fromName} <{from}>", to = new[] { m.To }, subject = m.Subject, text = m.Body };
        var req = new HttpRequestMessage(HttpMethod.Post, "https://api.resend.com/emails")
        { Content = JsonContent.Create(payload) };
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        return req;
    }

    private static HttpRequestMessage BuildBrevo(string apiKey, string from, string fromName, EmailMessage m)
    {
        var payload = new
        {
            sender = new { email = from, name = fromName },
            to = new[] { new { email = m.To } },
            subject = m.Subject,
            textContent = m.Body,
        };
        var req = new HttpRequestMessage(HttpMethod.Post, "https://api.brevo.com/v3/smtp/email")
        { Content = JsonContent.Create(payload) };
        req.Headers.Add("api-key", apiKey);
        return req;
    }

    private static string Trunc(string s) => s.Length > 300 ? s[..300] : s;
}
