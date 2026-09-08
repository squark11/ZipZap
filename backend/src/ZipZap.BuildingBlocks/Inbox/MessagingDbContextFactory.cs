using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ZipZap.BuildingBlocks.Inbox;

public sealed class MessagingDbContextFactory : IDesignTimeDbContextFactory<MessagingDbContext>
{
    public MessagingDbContext CreateDbContext(string[] args)
    {
        var conn = Environment.GetEnvironmentVariable("ConnectionStrings__Postgres")
                   ?? "Host=localhost;Port=5432;Database=zipzap;Username=zipzap;Password=zipzap";

        var options = new DbContextOptionsBuilder<MessagingDbContext>()
            .UseNpgsql(conn, npg => npg.MigrationsHistoryTable("__ef_migrations_history", MessagingDbContext.Schema))
            .Options;

        return new MessagingDbContext(options);
    }
}
