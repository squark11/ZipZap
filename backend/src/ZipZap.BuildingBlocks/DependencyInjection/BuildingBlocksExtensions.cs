using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ZipZap.BuildingBlocks.Inbox;
using ZipZap.BuildingBlocks.Messaging;
using ZipZap.BuildingBlocks.MultiTenancy;
using ZipZap.BuildingBlocks.Outbox;
using ZipZap.BuildingBlocks.Persistence;

namespace ZipZap.BuildingBlocks.DependencyInjection;

public static class BuildingBlocksExtensions
{
    /// <summary>
    /// Rejestruje wspólny rdzeń: rejestr typów zdarzeń, szynę zdarzeń (in-process
    /// lub RabbitMQ — zależnie od konfiguracji), kontekst najemcy i dispatcher outboxa.
    /// </summary>
    public static IServiceCollection AddBuildingBlocks(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<IIntegrationEventTypeRegistry>(sp =>
        {
            var registry = new IntegrationEventTypeRegistry();
            foreach (var registration in sp.GetServices<IntegrationEventTypeRegistration>())
                registry.Register(registration.EventType);
            return registry;
        });

        services.AddSingleton<IntegrationEventDispatcher>();

        // Szyna: RabbitMQ jeśli skonfigurowano host, inaczej in-process.
        var rabbit = configuration.GetSection(RabbitMqOptions.SectionName).Get<RabbitMqOptions>() ?? new RabbitMqOptions();
        services.AddSingleton(rabbit);

        if (rabbit.Enabled)
        {
            services.AddSingleton<RabbitMqConnection>();
            services.AddSingleton<IEventBus, RabbitMqEventBus>();
            services.AddHostedService<RabbitMqSubscriberHostedService>();
        }
        else
        {
            services.AddSingleton<IEventBus, InProcessEventBus>();
        }

        // Inbox konsumenta (deduplikacja) — schemat `messaging`, migrowany jak inne moduły.
        var conn = configuration.GetConnectionString("Postgres");
        if (!string.IsNullOrWhiteSpace(conn))
        {
            services.AddDbContext<MessagingDbContext>(o =>
                o.UseNpgsql(conn, npg => npg.MigrationsHistoryTable("__ef_migrations_history", MessagingDbContext.Schema)));
            services.AddScoped<IModuleDbMigrator, EfCoreModuleMigrator<MessagingDbContext>>();
            services.AddScoped<IInboxStore, EfInboxStore>();
        }

        services.AddScoped<CurrentTenant>();
        services.AddScoped<ICurrentTenant>(sp => sp.GetRequiredService<CurrentTenant>());

        services.AddHostedService<OutboxDispatcherHostedService>();

        return services;
    }
}
