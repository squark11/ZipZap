using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ZipZap.BuildingBlocks.Messaging;

/// <summary>
/// Rozsyła zdarzenie integracyjne do wszystkich zarejestrowanych handlerów.
/// Wspólne dla szyny in-process (publikacja = dyspozycja) oraz konsumenta
/// RabbitMQ (odbiór z brokera = dyspozycja). Tworzy własny scope DI.
/// </summary>
public sealed class IntegrationEventDispatcher
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<IntegrationEventDispatcher> _logger;

    public IntegrationEventDispatcher(IServiceScopeFactory scopeFactory, ILogger<IntegrationEventDispatcher> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task DispatchAsync(IIntegrationEvent integrationEvent, CancellationToken ct = default)
    {
        var eventType = integrationEvent.GetType();
        var handlerType = typeof(IIntegrationEventHandler<>).MakeGenericType(eventType);
        var handleMethod = handlerType.GetMethod(nameof(IIntegrationEventHandler<IIntegrationEvent>.HandleAsync))!;

        using var scope = _scopeFactory.CreateScope();
        var handlers = scope.ServiceProvider.GetServices(handlerType).ToList();

        _logger.LogDebug("Dyspozycja {EventType} do {Count} handlerów", eventType.Name, handlers.Count);

        foreach (var handler in handlers)
        {
            if (handler is null) continue;
            await (Task)handleMethod.Invoke(handler, new object[] { integrationEvent, ct })!;
        }
    }
}
