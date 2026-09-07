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
}
