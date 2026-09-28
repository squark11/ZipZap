using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ZipZap.BuildingBlocks.Inbox;

namespace ZipZap.BuildingBlocks.Messaging;

/// <summary>
/// Rozsyła zdarzenie integracyjne do wszystkich zarejestrowanych handlerów.
/// Wspólne dla szyny in-process (publikacja = dyspozycja) oraz konsumenta
/// RabbitMQ (odbiór z brokera = dyspozycja). Tworzy własny scope DI.
///
/// Deduplikacja per handler (inbox, klucz = Id zdarzenia + typ handlera): przy ponownym dostarczeniu tego samego
/// zdarzenia (ponowienie po awarii jednego z kilku handlerów, ręczne ponowienie z panelu, powtórka po awarii
/// instancji) handler, który już je obsłużył, jest pomijany. Handler, który zawiódł, dostaje zdarzenie ponownie.
/// Wąskie okno: skutek handlera zapisany, a znacznik w inboxie nie (awaria dokładnie w tej chwili) — dlatego
/// handlery nadal powinny być idempotentne same z siebie.
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
        var inbox = scope.ServiceProvider.GetService<IInboxStore>(); // brak bazy (np. testy jednostkowe) = bez deduplikacji

        _logger.LogDebug("Dyspozycja {EventType} do {Count} handlerów", eventType.Name, handlers.Count);

        foreach (var handler in handlers)
        {
            if (handler is null) continue;
            var handlerName = handler.GetType().Name;
            var key = HandlerInboxKey(integrationEvent.Id, handler.GetType());

            if (inbox is not null && await inbox.HasProcessedAsync(key, ct))
            {
                _logger.LogInformation("Pominięto powtórzone zdarzenie {EventId} ({EventType}) — {Handler} już je obsłużył.",
                    integrationEvent.Id, eventType.Name, handlerName);
                continue;
            }

            await (Task)handleMethod.Invoke(handler, new object[] { integrationEvent, ct })!;

            if (inbox is not null)
                await inbox.MarkProcessedAsync(key, Truncate($"handler:{handlerName}:{eventType.Name}", 300), ct);
        }
    }

    /// <summary>Deterministyczny klucz inboxa dla pary (zdarzenie, handler) — różny od samego Id zdarzenia.</summary>
    public static Guid HandlerInboxKey(Guid eventId, Type handlerType)
    {
        var bytes = MD5.HashData(Encoding.UTF8.GetBytes($"{eventId:N}|{handlerType.FullName}"));
        return new Guid(bytes);
    }

    private static string Truncate(string s, int max) => s.Length <= max ? s : s[..max];
}
