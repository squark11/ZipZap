using Microsoft.Extensions.DependencyInjection;
using ZipZap.BuildingBlocks.Messaging;
using ZipZap.BuildingBlocks.MultiTenancy;
using ZipZap.BuildingBlocks.Outbox;

namespace ZipZap.BuildingBlocks.DependencyInjection;

public static class BuildingBlocksExtensions
{
    /// <summary>
    /// Rejestruje wspólny rdzeń: rejestr typów zdarzeń, in-process event bus,
    /// kontekst najemcy oraz dispatcher outboxa.
    /// </summary>
    public static IServiceCollection AddBuildingBlocks(this IServiceCollection services)
    {
        services.AddSingleton<IIntegrationEventTypeRegistry>(sp =>
        {
            var registry = new IntegrationEventTypeRegistry();
            foreach (var registration in sp.GetServices<IntegrationEventTypeRegistration>())
                registry.Register(registration.EventType);
            return registry;
        });
        services.AddSingleton<IEventBus, InProcessEventBus>();

        services.AddScoped<CurrentTenant>();
        services.AddScoped<ICurrentTenant>(sp => sp.GetRequiredService<CurrentTenant>());

        services.AddHostedService<OutboxDispatcherHostedService>();

        return services;
    }
}
