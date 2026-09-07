namespace ZipZap.BuildingBlocks.Messaging;

/// <summary>
/// Handler zdarzenia integracyjnego. Moduł-konsument rejestruje implementacje
/// jako scoped; muszą być IDEMPOTENTNE (dostawa at-least-once z outboxa).
/// </summary>
public interface IIntegrationEventHandler<in TEvent> where TEvent : IIntegrationEvent
{
    Task HandleAsync(TEvent integrationEvent, CancellationToken ct = default);
}
