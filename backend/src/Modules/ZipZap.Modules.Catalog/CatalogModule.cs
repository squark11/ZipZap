using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ZipZap.BuildingBlocks.Messaging;
using ZipZap.BuildingBlocks.Outbox;
using ZipZap.BuildingBlocks.Persistence;
using ZipZap.Modules.Catalog.Application;
using ZipZap.Modules.Catalog.Contracts;
using ZipZap.Modules.Catalog.Infrastructure;

namespace ZipZap.Modules.Catalog;

public static class CatalogModule
{
    public static IServiceCollection AddCatalogModule(this IServiceCollection services, IConfiguration config)
    {
        var conn = config.GetConnectionString("Postgres")
            ?? throw new InvalidOperationException("Brak connection stringa 'Postgres'.");

        services.AddDbContext<CatalogDbContext>(o =>
            o.UseNpgsql(conn, npg =>
                npg.MigrationsHistoryTable("__ef_migrations_history", CatalogDbContext.Schema)));

        services.AddScoped<IOutboxProcessor, OutboxProcessor<CatalogDbContext>>();
        services.AddScoped<IModuleDbMigrator, EfCoreModuleMigrator<CatalogDbContext>>();
        services.AddScoped<CatalogService>();

        services.RegisterIntegrationEventType<StoreRegistered>();
        services.RegisterIntegrationEventType<StoreUpdated>();
        services.RegisterIntegrationEventType<ProductPublished>();
        services.RegisterIntegrationEventType<ProductUpdated>();

        return services;
    }
}
