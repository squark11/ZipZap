using ZipZap.BuildingBlocks.Domain;

namespace ZipZap.Modules.Feedback.Domain;

public enum FeedbackType { Bug, Idea, Other }
public enum FeedbackStatus { New, InProgress, Closed }

/// <summary>Uwaga/zgłoszenie użytkownika aplikacji — „co poprawić". Zbierane w apce i panelu.</summary>
public sealed class FeedbackItem : Entity
{
    public FeedbackType Type { get; private set; }
    public string Message { get; private set; } = default!;
    public Guid? UserId { get; private set; }
    public string? ContactEmail { get; private set; }
    public Guid? StoreId { get; private set; }
    public string? Screen { get; private set; }
    public string? AppVersion { get; private set; }
    public string? Platform { get; private set; }
    public FeedbackStatus Status { get; private set; }
    public DateTime CreatedAtUtc { get; private set; } = DateTime.UtcNow;

    private FeedbackItem() { } // EF

    public FeedbackItem(FeedbackType type, string message, Guid? userId, string? contactEmail,
        Guid? storeId, string? screen, string? appVersion, string? platform)
    {
        Type = type;
        Message = message.Trim();
        UserId = userId;
        ContactEmail = string.IsNullOrWhiteSpace(contactEmail) ? null : contactEmail.Trim();
        StoreId = storeId;
        Screen = string.IsNullOrWhiteSpace(screen) ? null : screen.Trim();
        AppVersion = string.IsNullOrWhiteSpace(appVersion) ? null : appVersion.Trim();
        Platform = string.IsNullOrWhiteSpace(platform) ? null : platform.Trim();
        Status = FeedbackStatus.New;
    }

    public void SetStatus(FeedbackStatus status) => Status = status;
}
