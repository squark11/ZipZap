using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ZipZap.BuildingBlocks.Messaging;
using ZipZap.BuildingBlocks.Outbox;
using ZipZap.BuildingBlocks.Persistence;
using ZipZap.Contracts.Catalog;
using ZipZap.Contracts.Ordering;
using ZipZap.Contracts.Payments;
using ZipZap.Modules.Ordering.Application;
using ZipZap.Modules.Ordering.Application.EventHandlers;
using ZipZap.Modules.Ordering.Infrastructure;

namespace ZipZap.Modules.Ordering;

public static class OrderingModule
{
    public static IServiceCollection AddOrderingModule(this IServiceCollection services, IConfiguration config)
    {
        var conn = config.GetConnectionString("Postgres")
            ?? throw new InvalidOperationException("Brak connection stringa 'Postgres'.");

        services.AddDbContext<OrderingDbContext>(o =>
            o.UseNpgsql(conn, npg =>
                npg.MigrationsHistoryTable("__ef_migrations_history", OrderingDbContext.Schema)));

        services.AddScoped<IOutboxProcessor, OutboxProcessor<OrderingDbContext>>();
        services.AddScoped<IModuleDbMigrator, EfCoreModuleMigrator<OrderingDbContext>>();
        services.AddScoped<OrderingService>();

        // Konsumpcja zdarzeń Catalog → lokalny read-model (cross-module tylko przez eventy).
        services.AddScoped<CatalogStoreProjectionHandler>();
        services.AddScoped<IIntegrationEventHandler<StoreRegistered>>(sp => sp.GetRequiredService<CatalogStoreProjectionHandler>());
        services.AddScoped<IIntegrationEventHandler<StoreUpdated>>(sp => sp.GetRequiredService<CatalogStoreProjectionHandler>());

        services.AddScoped<CatalogProductProjectionHandler>();
        services.AddScoped<IIntegrationEventHandler<ProductPublished>>(sp => sp.GetRequiredService<CatalogProductProjectionHandler>());
        services.AddScoped<IIntegrationEventHandler<ProductUpdated>>(sp => sp.GetRequiredService<CatalogProductProjectionHandler>());

        // Choreografia płatności: PaymentAuthorized → potwierdzenie zamówienia.
        services.AddScoped<PaymentAuthorizedHandler>();
        services.AddScoped<IIntegrationEventHandler<PaymentAuthorized>>(sp => sp.GetRequiredService<PaymentAuthorizedHandler>());

        // Produkowane zdarzenia (dla outboxa).
        services.RegisterIntegrationEventType<OrderPlaced>();
        services.RegisterIntegrationEventType<OrderReadyForPickup>();
        services.RegisterIntegrationEventType<OrderPickedUp>();
        services.RegisterIntegrationEventType<OrderDelivered>();
        services.RegisterIntegrationEventType<OrderCancelled>();

        return services;
    }
}
