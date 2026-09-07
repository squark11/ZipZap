using System.Text.Json;
using ZipZap.BuildingBlocks.Messaging;

namespace ZipZap.BuildingBlocks.Outbox;

/// <summary>
/// Pomocnik do wrzucania zdarzeń integracyjnych do outboxa modułu.
/// Wywoływany wewnątrz jednostki pracy modułu (przed SaveChanges).
/// </summary>
public static class OutboxWriterExtensions
{
    public static void AddOutboxMessage(
        this IOutboxDbContext db,
        IIntegrationEvent @event,
        IIntegrationEventTypeRegistry registry)
    {
        db.OutboxMessages.Add(new OutboxMessage
        {
            Id = @event.Id,
            Type = registry.GetName(@event.GetType()),
            Payload = JsonSerializer.Serialize(@event, @event.GetType()),
            OccurredAtUtc = @event.OccurredAtUtc,
        });
    }
}
