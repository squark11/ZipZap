using Microsoft.Extensions.Logging;
using ZipZap.Modules.Notifications.Domain;

namespace ZipZap.Modules.Notifications.Application;

/// <summary>Port kanału powiadomień (przyszłe adaptery: e-mail, SMS, push).</summary>
public interface INotificationChannel
{
    string Name { get; }
    Task SendAsync(Notification notification, CancellationToken ct = default);
}

/// <summary>Mockowy kanał no-op — loguje powiadomienie zamiast realnie je wysyłać.</summary>
public sealed class LoggingNotificationChannel : INotificationChannel
{
    private readonly ILogger<LoggingNotificationChannel> _logger;

    public LoggingNotificationChannel(ILogger<LoggingNotificationChannel> logger) => _logger = logger;

    public string Name => "mock";

    public Task SendAsync(Notification notification, CancellationToken ct = default)
    {
        _logger.LogInformation("[NOTIFY:{Template}] recipient={Recipient} payload={Payload}",
            notification.Template, notification.RecipientUserId, notification.Payload);
        return Task.CompletedTask;
    }
}
