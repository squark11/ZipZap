using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ZipZap.BuildingBlocks.Messaging;

/// <summary>
/// In-process implementacja szyny. Dla każdej publikacji tworzy nowy scope DI
/// i wywołuje wszystkie zarejestrowane handlery danego typu zdarzenia.
/// </summary>
public sealed class InProcessEventBus : IEventBus
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<InProcessEventBus> _logger;

    public InProcessEventBus(IServiceScopeFactory scopeFactory, ILogger<InProcessEventBus> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task PublishAsync(IIntegrationEvent integrationEvent, CancellationToken ct = default)
    {
        var eventType = integrationEvent.GetType();
        var handlerType = typeof(IIntegrationEventHandler<>).MakeGenericType(eventType);
        var handleMethod = handlerType.GetMethod(nameof(IIntegrationEventHandler<IIntegrationEvent>.HandleAsync))!;

        using var scope = _scopeFactory.CreateScope();
        var handlers = scope.ServiceProvider.GetServices(handlerType).ToList();

        _logger.LogDebug("Publikuję {EventType} do {Count} handlerów", eventType.Name, handlers.Count);

        foreach (var handler in handlers)
        {
            if (handler is null) continue;
            await (Task)handleMethod.Invoke(handler, new object[] { integrationEvent, ct })!;
        }
    }
}
