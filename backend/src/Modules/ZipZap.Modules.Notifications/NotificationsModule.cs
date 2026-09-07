using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ZipZap.BuildingBlocks.Messaging;
using ZipZap.BuildingBlocks.Persistence;
using ZipZap.Contracts.Identity;
using ZipZap.Contracts.Ordering;
using ZipZap.Contracts.Payments;
using ZipZap.Modules.Notifications.Application;
using ZipZap.Modules.Notifications.Infrastructure;

namespace ZipZap.Modules.Notifications;

public static class NotificationsModule
{
    public static IServiceCollection AddNotificationsModule(this IServiceCollection services, IConfiguration config)
    {
        var conn = config.GetConnectionString("Postgres")
            ?? throw new InvalidOperationException("Brak connection stringa 'Postgres'.");

        services.AddDbContext<NotificationsDbContext>(o =>
            o.UseNpgsql(conn, npg => npg.MigrationsHistoryTable("__ef_migrations_history", NotificationsDbContext.Schema)));

        services.AddScoped<IModuleDbMigrator, EfCoreModuleMigrator<NotificationsDbContext>>();
        services.AddSingleton<INotificationChannel, LoggingNotificationChannel>();

        services.AddScoped<NotificationsEventHandlers>();
        services.AddScoped<IIntegrationEventHandler<CustomerRegistered>>(sp => sp.GetRequiredService<NotificationsEventHandlers>());
        services.AddScoped<IIntegrationEventHandler<OrderPlaced>>(sp => sp.GetRequiredService<NotificationsEventHandlers>());
        services.AddScoped<IIntegrationEventHandler<PaymentAuthorized>>(sp => sp.GetRequiredService<NotificationsEventHandlers>());
        services.AddScoped<IIntegrationEventHandler<OrderReadyForPickup>>(sp => sp.GetRequiredService<NotificationsEventHandlers>());
        services.AddScoped<IIntegrationEventHandler<OrderDelivered>>(sp => sp.GetRequiredService<NotificationsEventHandlers>());

        return services;
    }
}
