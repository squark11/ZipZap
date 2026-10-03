using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ZipZap.Api.Configuration;

/// <summary>
/// Konfiguracja platformy i sklepów edytowana w panelu + klucze Data Protection — schemat `platform`.
/// Wcześniej pliki w App_Data, które na hostingu bez trwałego dysku (Render free) znikały przy każdym wdrożeniu
/// razem z kluczami potrzebnymi do odszyfrowania sekretów.
/// </summary>
public sealed class PlatformConfigDbContext : DbContext, IDataProtectionKeyContext
{
    public const string Schema = "platform";

    public PlatformConfigDbContext(DbContextOptions<PlatformConfigDbContext> options) : base(options) { }

    public DbSet<ConfigDocument> Documents => Set<ConfigDocument>();
    public DbSet<DataProtectionKey> DataProtectionKeys => Set<DataProtectionKey>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.HasDefaultSchema(Schema);

        b.Entity<ConfigDocument>(e =>
        {
            e.ToTable("config_documents");
            e.HasKey(x => x.Key);
            e.Property(x => x.Key).HasMaxLength(200);
            e.Property(x => x.Json).HasColumnType("jsonb").IsRequired();
        });

        b.Entity<DataProtectionKey>(e => e.ToTable("data_protection_keys"));
    }
}

/// <summary>Jeden dokument konfiguracji (JSON), np. „platform-integrations" albo „store-legal:{storeId}".</summary>
public sealed class ConfigDocument
{
    public string Key { get; private set; } = null!;
    public string Json { get; private set; } = null!;
    public DateTime UpdatedAtUtc { get; private set; }

    private ConfigDocument() { }

    public ConfigDocument(string key, string json, DateTime nowUtc)
    {
        Key = key;
        Json = json;
        UpdatedAtUtc = nowUtc;
    }

    public void Replace(string json, DateTime nowUtc)
    {
        Json = json;
        UpdatedAtUtc = nowUtc;
    }
}

public sealed class PlatformConfigDbContextFactory : IDesignTimeDbContextFactory<PlatformConfigDbContext>
{
    public PlatformConfigDbContext CreateDbContext(string[] args)
    {
        var conn = Environment.GetEnvironmentVariable("ConnectionStrings__Postgres")
                   ?? "Host=localhost;Port=5432;Database=zipzap;Username=zipzap;Password=zipzap";

        var options = new DbContextOptionsBuilder<PlatformConfigDbContext>()
            .UseNpgsql(conn, npg => npg.MigrationsHistoryTable("__ef_migrations_history", PlatformConfigDbContext.Schema))
            .Options;

        return new PlatformConfigDbContext(options);
    }
}
