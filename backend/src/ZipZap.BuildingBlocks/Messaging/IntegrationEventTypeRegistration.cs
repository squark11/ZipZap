using Microsoft.Extensions.DependencyInjection;

namespace ZipZap.BuildingBlocks.Messaging;

/// <summary>
/// Deklaracja typu zdarzenia integracyjnego wnoszona do DI przez moduł.
/// Zbierane przy budowie <see cref="IIntegrationEventTypeRegistry"/>.
/// </summary>
public sealed record IntegrationEventTypeRegistration(Type EventType);

public static class IntegrationEventRegistrationExtensions
{
    /// <summary>Rejestruje typ zdarzenia integracyjnego modułu (do (de)serializacji outboxa).</summary>
    public static IServiceCollection RegisterIntegrationEventType<TEvent>(this IServiceCollection services)
        where TEvent : IIntegrationEvent
    {
        services.AddSingleton(new IntegrationEventTypeRegistration(typeof(TEvent)));
        return services;
    }
}
