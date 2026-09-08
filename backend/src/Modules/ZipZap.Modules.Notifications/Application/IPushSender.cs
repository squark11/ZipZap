namespace ZipZap.Modules.Notifications.Application;

public sealed record PushMessage(Guid RecipientUserId, string Title, string Body, IReadOnlyList<string> DeviceTokens);

/// <summary>
/// Port powiadomień push. Realny adapter (Firebase Cloud Messaging / APNs)
/// dodasz później; teraz działa mock logujący. Wymaga tokenów urządzeń użytkownika.
/// </summary>
public interface IPushSender
{
    Task SendAsync(PushMessage message, CancellationToken ct = default);
}
