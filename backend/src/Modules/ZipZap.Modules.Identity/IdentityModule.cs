using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ZipZap.BuildingBlocks.Messaging;
using ZipZap.BuildingBlocks.Outbox;
using ZipZap.BuildingBlocks.Persistence;
using ZipZap.Modules.Identity.Application;
using ZipZap.Modules.Identity.Contracts;
using ZipZap.Modules.Identity.Infrastructure;

namespace ZipZap.Modules.Identity;

public static class IdentityModule
{
    public static IServiceCollection AddIdentityModule(this IServiceCollection services, IConfiguration config)
    {
        var conn = config.GetConnectionString("Postgres")
            ?? throw new InvalidOperationException("Brak connection stringa 'Postgres'.");

        services.AddDbContext<IdentityDbContext>(o =>
            o.UseNpgsql(conn, npg =>
                npg.MigrationsHistoryTable("__ef_migrations_history", IdentityDbContext.Schema)));

        services.AddScoped<IOutboxProcessor, OutboxProcessor<IdentityDbContext>>();
        services.AddScoped<IModuleDbMigrator, EfCoreModuleMigrator<IdentityDbContext>>();

        services.Configure<JwtOptions>(config.GetSection(JwtOptions.SectionName));
        services.AddScoped<IPasswordHasher, Pbkdf2PasswordHasher>();
        services.AddSingleton<ITokenService, JwtTokenService>();
        services.AddScoped<IdentityService>();

        services.RegisterIntegrationEventType<CustomerRegistered>();

        return services;
    }
}
