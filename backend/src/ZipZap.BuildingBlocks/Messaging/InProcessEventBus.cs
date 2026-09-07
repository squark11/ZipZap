namespace ZipZap.BuildingBlocks.Messaging;

/// <summary>
/// In-process implementacja szyny: publikacja = natychmiastowa dyspozycja do handlerów.
/// Używana lokalnie / w testach (gdy broker nie jest skonfigurowany).
/// </summary>
public sealed class InProcessEventBus : IEventBus
{
    private readonly IntegrationEventDispatcher _dispatcher;

    public InProcessEventBus(IntegrationEventDispatcher dispatcher) => _dispatcher = dispatcher;

    public Task PublishAsync(IIntegrationEvent integrationEvent, CancellationToken ct = default)
        => _dispatcher.DispatchAsync(integrationEvent, ct);
}
