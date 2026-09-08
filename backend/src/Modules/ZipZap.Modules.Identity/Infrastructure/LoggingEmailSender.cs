using Microsoft.Extensions.Logging;
using ZipZap.Modules.Identity.Application;

namespace ZipZap.Modules.Identity.Infrastructure;

/// <summary>
/// Mockowy sender: loguje e-mail zamiast wysyłać (dev/test). Realny SMTP/dostawca
/// = przyszły adapter. NIE loguje haseł; token pojawia się w linku wiadomości.
/// </summary>
public sealed class LoggingEmailSender : IEmailSender
{
    private readonly ILogger<LoggingEmailSender> _logger;

    public LoggingEmailSender(ILogger<LoggingEmailSender> logger) => _logger = logger;

    public Task SendAsync(EmailMessage message, CancellationToken ct = default)
    {
        _logger.LogInformation("[EMAIL] to={To} subject={Subject}\n{Body}", message.To, message.Subject, message.Body);
        return Task.CompletedTask;
    }
}
