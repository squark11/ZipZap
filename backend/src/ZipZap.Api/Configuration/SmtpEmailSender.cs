using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using ZipZap.BuildingBlocks.Logging;
using ZipZap.Modules.Identity.Application;

namespace ZipZap.Api.Configuration;

/// <summary>
/// Realny sender SMTP (MailKit). Konfiguracja z panelu (<see cref="PlatformIntegrationsStore"/>),
/// hasło szyfrowane at-rest. Gdy SMTP nieustawiony — loguje (fallback), żeby dev/test działał bez poczty.
/// </summary>
public sealed class SmtpEmailSender : IEmailSender
{
    private readonly PlatformIntegrationsStore _store;
    private readonly IConfiguration _config;
    private readonly ILogger<SmtpEmailSender> _logger;

    public SmtpEmailSender(PlatformIntegrationsStore store, IConfiguration config, ILogger<SmtpEmailSender> logger)
    {
        _store = store;
        _config = config;
        _logger = logger;
    }

    public async Task SendAsync(EmailMessage message, CancellationToken ct = default)
    {
        // Priorytet: konfiguracja z panelu; gdy pusta — z env (Email:Smtp:*).
        var cfg = await _store.GetSmtpAsync(ct);
        if (!cfg.Enabled) cfg = SmtpFromEnv();
        if (!cfg.Enabled)
        {
            _logger.LogInformation("[EMAIL:mock] SMTP nieustawiony (panel ani env). to={To} subject={Subject}",
                EmailLog.Mask(message.To), message.Subject);
            return;
        }

        var msg = new MimeMessage();
        msg.From.Add(new MailboxAddress(cfg.FromName ?? "Dowózka.pl", cfg.FromEmail));
        msg.To.Add(MailboxAddress.Parse(message.To));
        msg.Subject = message.Subject;
        msg.Body = new TextPart("plain") { Text = message.Body };

        using var client = new SmtpClient();
        // 465 = SSL bezpośredni (SslOnConnect); inaczej STARTTLS.
        var socket = cfg.UseSsl || cfg.Port == 465 ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTls;
        await client.ConnectAsync(cfg.Host, cfg.Port, socket, ct);
        if (!string.IsNullOrWhiteSpace(cfg.Username))
            await client.AuthenticateAsync(cfg.Username, cfg.Password ?? string.Empty, ct);
        await client.SendAsync(msg, ct);
        await client.DisconnectAsync(true, ct);
        _logger.LogInformation("[EMAIL] wysłano to={To} subject={Subject}", EmailLog.Mask(message.To), message.Subject);
    }

    /// <summary>Konfiguracja SMTP z env (Email:Smtp:*), gdy panel nieustawiony. Port domyślny 465, SSL domyślnie on.</summary>
    private SmtpConfig SmtpFromEnv()
    {
        var host = _config["Email:Smtp:Host"];
        if (string.IsNullOrWhiteSpace(host)) return new SmtpConfig(null, 0, true, null, null, null, null);
        var port = int.TryParse(_config["Email:Smtp:Port"], out var p) && p > 0 ? p : 465;
        var useSsl = !bool.TryParse(_config["Email:Smtp:UseSsl"], out var s) || s;
        var from = _config["Email:Smtp:FromEmail"] ?? _config["Email:Smtp:Username"];
        return new SmtpConfig(host, port, useSsl,
            _config["Email:Smtp:Username"], _config["Email:Smtp:Password"], from, _config["Email:Smtp:FromName"]);
    }
}
