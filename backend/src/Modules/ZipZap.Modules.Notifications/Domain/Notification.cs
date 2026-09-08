using ZipZap.BuildingBlocks.Domain;

namespace ZipZap.Modules.Notifications.Domain;

/// <summary>Powiadomienie (szkielet). Realne kanały (e-mail/SMS/push) = przyszłe adaptery.</summary>
public sealed class Notification : Entity
{
    public Guid? RecipientUserId { get; private set; }
    public string Channel { get; private set; } = "mock";
    public string Template { get; private set; } = default!;
    public string Payload { get; private set; } = default!;
    public string Status { get; private set; } = "Pending";
    public bool IsRead { get; private set; }
    public DateTime CreatedAtUtc { get; private set; } = DateTime.UtcNow;
    public DateTime? SentAtUtc { get; private set; }

    private Notification() { } // EF

    public Notification(Guid? recipientUserId, string channel, string template, string payload)
    {
        RecipientUserId = recipientUserId;
        Channel = channel;
        Template = template;
        Payload = payload;
    }

    public void MarkSent()
    {
        Status = "Sent";
        SentAtUtc = DateTime.UtcNow;
    }

    public void MarkRead() => IsRead = true;
}

/// <summary>Token urządzenia (push/FCM) zarejestrowany przez użytkownika.</summary>
public sealed class DeviceToken : Entity
{
    public Guid UserId { get; private set; }
    public string Token { get; private set; } = default!;
    public string Platform { get; private set; } = "android";
    public DateTime CreatedAtUtc { get; private set; } = DateTime.UtcNow;

    private DeviceToken() { } // EF

    public DeviceToken(Guid userId, string token, string platform)
    {
        UserId = userId;
        Token = token;
        Platform = platform;
    }
}

/// <summary>Lokalny read-model: kto jest odbiorcą (klientem) danego zamówienia.</summary>
public sealed class OrderRecipient
{
    public Guid OrderId { get; set; }
    public Guid CustomerId { get; set; }
}
