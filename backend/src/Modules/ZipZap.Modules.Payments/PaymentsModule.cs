using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ZipZap.BuildingBlocks.Messaging;
using ZipZap.BuildingBlocks.Outbox;
using ZipZap.BuildingBlocks.Persistence;
using ZipZap.Contracts.Ordering;
using ZipZap.Contracts.Payments;
using ZipZap.Modules.Payments.Application;
using ZipZap.Modules.Payments.Infrastructure;

namespace ZipZap.Modules.Payments;

public static class PaymentsModule
{
    public static IServiceCollection AddPaymentsModule(this IServiceCollection services, IConfiguration config)
    {
        var conn = config.GetConnectionString("Postgres")
            ?? throw new InvalidOperationException("Brak connection stringa 'Postgres'.");

        services.AddDbContext<PaymentsDbContext>(o =>
            o.UseNpgsql(conn, npg => npg.MigrationsHistoryTable("__ef_migrations_history", PaymentsDbContext.Schema)));

        services.AddScoped<IOutboxProcessor, OutboxProcessor<PaymentsDbContext>>();
        services.AddScoped<IModuleDbMigrator, EfCoreModuleMigrator<PaymentsDbContext>>();

        // Dostawcy płatności (abstrakcja) + rejestr z domyślnym kluczem z konfiguracji.
        services.Configure<PaymentsOptions>(config.GetSection(PaymentsOptions.SectionName));
        // Mock (z jawnym w repo sekretem `mock-dev-secret`) NIE istnieje w trybie publicznego pilotażu:
        // brak sesji mocka i brak weryfikatora dla /api/payments/webhook/mock (→ 404). Zamówienia
        // w pilotażu są testowe i nieksięgowe (patrz PaymentsEventHandlers).
        if (!config.GetValue<bool>("Pilot:Public", false))
            services.AddSingleton<IPaymentProvider, MockPaymentProvider>();
        services.AddSingleton(sp => new PaymentProviderRegistry(
            sp.GetServices<IPaymentProvider>(),
            sp.GetRequiredService<IOptions<PaymentsOptions>>().Value.Provider));

        // Domyślny resolver per-store bramki (host może nadpisać realnym adapterem).
        services.AddSingleton<IStorePaymentGateway, NullStorePaymentGateway>();

        services.AddScoped<PaymentsEventHandlers>();
        services.AddScoped<IIntegrationEventHandler<OrderPlaced>>(sp => sp.GetRequiredService<PaymentsEventHandlers>());
        services.AddScoped<IIntegrationEventHandler<OrderDelivered>>(sp => sp.GetRequiredService<PaymentsEventHandlers>());

        services.RegisterIntegrationEventType<PaymentAuthorized>();
        services.RegisterIntegrationEventType<PaymentFailed>();

        return services;
    }
}
