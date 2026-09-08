using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ZipZap.BuildingBlocks.Messaging;
using ZipZap.BuildingBlocks.Outbox;
using ZipZap.BuildingBlocks.Persistence;
using ZipZap.Contracts.Ordering;
using ZipZap.Modules.Delivery.Application;
using ZipZap.Modules.Delivery.Infrastructure;

namespace ZipZap.Modules.Delivery;

public static class DeliveryModule
{
    public static IServiceCollection AddDeliveryModule(this IServiceCollection services, IConfiguration config)
    {
        var conn = config.GetConnectionString("Postgres")
            ?? throw new InvalidOperationException("Brak connection stringa 'Postgres'.");

        services.AddDbContext<DeliveryDbContext>(o =>
            o.UseNpgsql(conn, npg => npg.MigrationsHistoryTable("__ef_migrations_history", DeliveryDbContext.Schema)));

        services.AddScoped<IOutboxProcessor, OutboxProcessor<DeliveryDbContext>>();
        services.AddScoped<IModuleDbMigrator, EfCoreModuleMigrator<DeliveryDbContext>>();
        services.AddScoped<DeliveryService>();

        // Konsumuje: gotowość do odbioru → utworzenie dostawy w puli.
        services.AddScoped<DeliveryEventHandlers>();
        services.AddScoped<IIntegrationEventHandler<OrderReadyForPickup>>(sp => sp.GetRequiredService<DeliveryEventHandlers>());

        // Produkuje: odbiór i dostarczenie (Ordering je konsumuje, by przesunąć stan).
        services.RegisterIntegrationEventType<OrderPickedUp>();
        services.RegisterIntegrationEventType<OrderDelivered>();

        return services;
    }
}
