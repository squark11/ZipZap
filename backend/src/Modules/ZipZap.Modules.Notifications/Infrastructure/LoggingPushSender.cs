using Microsoft.Extensions.Logging;
using ZipZap.Modules.Notifications.Application;

namespace ZipZap.Modules.Notifications.Infrastructure;

/// <summary>Mock push: loguje zamiast wysyłać do FCM/APNs (dev/test).</summary>
public sealed class LoggingPushSender : IPushSender
{
    private readonly ILogger<LoggingPushSender> _logger;

    public LoggingPushSender(ILogger<LoggingPushSender> logger) => _logger = logger;

    public Task SendAsync(PushMessage message, CancellationToken ct = default)
    {
        if (message.DeviceTokens.Count == 0)
        {
            _logger.LogInformation("[PUSH] user={User} bez zarejestrowanych urządzeń — pomijam ({Title})",
                message.RecipientUserId, message.Title);
            return Task.CompletedTask;
        }
        _logger.LogInformation("[PUSH] user={User} tokens={Count} title=\"{Title}\" body=\"{Body}\"",
            message.RecipientUserId, message.DeviceTokens.Count, message.Title, message.Body);
        return Task.CompletedTask;
    }
}
