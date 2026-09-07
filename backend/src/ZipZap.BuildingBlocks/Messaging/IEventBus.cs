namespace ZipZap.BuildingBlocks.Messaging;

/// <summary>
/// Szyna zdarzeń. Na start in-process (<see cref="InProcessEventBus"/>);
/// docelowo do podmiany na brokera (RabbitMQ/Kafka) bez zmian w modułach.
/// </summary>
public interface IEventBus
{
    Task PublishAsync(IIntegrationEvent integrationEvent, CancellationToken ct = default);
}
