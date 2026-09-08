using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ZipZap.BuildingBlocks.Messaging;
using ZipZap.BuildingBlocks.Outbox;
using ZipZap.BuildingBlocks.Persistence;
using ZipZap.Contracts.Identity;
using ZipZap.Modules.Identity.Application;
using ZipZap.Modules.Identity.Domain;
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
        services.Configure<IdentityOptions>(config.GetSection(IdentityOptions.SectionName));
        services.Configure<GoogleOptions>(config.GetSection(GoogleOptions.SectionName));
        services.AddScoped<IPasswordHasher, Pbkdf2PasswordHasher>();
        services.AddSingleton<ITokenService, JwtTokenService>();
        services.AddSingleton<IEmailSender, LoggingEmailSender>();
        services.AddSingleton<IGoogleTokenValidator, GoogleTokenValidator>();
        services.AddScoped<IdentityService>();

        services.RegisterIntegrationEventType<CustomerRegistered>();

        return services;
    }

    /// <summary>
    /// DEV-only: zakłada domyślnego administratora, jeśli żaden nie istnieje.
    /// Umożliwia bootstrap platformy (potem twórz kolejnych przez /api/identity/admin/users).
    /// </summary>
    public static async Task SeedDevelopmentAdminAsync(
        IServiceProvider services, string email, string password)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();

        var adminExists = await db.Users.AnyAsync(u => u.Roles.Any(r => r.Role == Role.Admin));
        if (adminExists) return;

        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
        var admin = User.Register(email, hasher.Hash(password), "ZipZap Admin", null, Role.Admin);
        db.Users.Add(admin);
        await db.SaveChangesAsync();
    }
}
