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
        // Bez ładunku: bywa w nim e-mail i imię klienta (np. customer.welcome) — w logu tylko szablon i id odbiorcy.
        _logger.LogInformation("[NOTIFY:{Template}] recipient={Recipient} notification={NotificationId}",
            notification.Template, notification.RecipientUserId, notification.Id);
        return Task.CompletedTask;
    }
}
