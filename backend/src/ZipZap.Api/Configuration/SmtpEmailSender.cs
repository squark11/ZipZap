using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using ZipZap.Modules.Identity.Application;

namespace ZipZap.Api.Configuration;

/// <summary>
/// Realny sender SMTP (MailKit). Konfiguracja z panelu (<see cref="PlatformIntegrationsStore"/>),
/// hasło szyfrowane at-rest. Gdy SMTP nieustawiony — loguje (fallback), żeby dev/test działał bez poczty.
/// </summary>
public sealed class SmtpEmailSender : IEmailSender
{
    private readonly PlatformIntegrationsStore _store;
    private readonly ILogger<SmtpEmailSender> _logger;

    public SmtpEmailSender(PlatformIntegrationsStore store, ILogger<SmtpEmailSender> logger)
    {
        _store = store;
        _logger = logger;
    }

    public async Task SendAsync(EmailMessage message, CancellationToken ct = default)
    {
        var cfg = await _store.GetSmtpAsync(ct);
        if (!cfg.Enabled)
        {
            _logger.LogInformation("[EMAIL:mock] SMTP nieustawiony w panelu. to={To} subject={Subject}",
                message.To, message.Subject);
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
        _logger.LogInformation("[EMAIL] wysłano to={To} subject={Subject}", message.To, message.Subject);
    }
}
