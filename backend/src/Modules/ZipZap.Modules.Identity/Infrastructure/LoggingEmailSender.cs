using Microsoft.Extensions.Logging;
using ZipZap.BuildingBlocks.Logging;
using ZipZap.Modules.Identity.Application;

namespace ZipZap.Modules.Identity.Infrastructure;

/// <summary>
/// Mockowy sender (domyślny w module; host nadpisuje go realnym senderem HTTP/SMTP).
/// NIE loguje treści wiadomości — linki weryfikacji/resetu zawierają jednorazowe tokeny,
/// które nie mogą trafić do logów. Adres odbiorcy jest maskowany.
/// </summary>
public sealed class LoggingEmailSender : IEmailSender
{
    private readonly ILogger<LoggingEmailSender> _logger;

    public LoggingEmailSender(ILogger<LoggingEmailSender> logger) => _logger = logger;

    public Task SendAsync(EmailMessage message, CancellationToken ct = default)
    {
        _logger.LogInformation("[EMAIL:mock] to={To} subject={Subject} (treść pominięta — zawiera tokeny)",
            EmailLog.Mask(message.To), message.Subject);
        return Task.CompletedTask;
    }
}
