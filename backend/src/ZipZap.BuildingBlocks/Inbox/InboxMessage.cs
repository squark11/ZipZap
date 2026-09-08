namespace ZipZap.BuildingBlocks.Inbox;

/// <summary>
/// Wpis inboxa — zdarzenie już przetworzone przez konsumenta. Chroni przed
/// ponownym przetworzeniem przy redostarczeniu (at-least-once / retry).
/// </summary>
public sealed class InboxMessage
{
    public Guid Id { get; set; }               // = Id zdarzenia integracyjnego
    public string Type { get; set; } = default!;
    public DateTime ProcessedAtUtc { get; set; } = DateTime.UtcNow;
}
