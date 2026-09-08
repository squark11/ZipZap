using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using ZipZap.BuildingBlocks.MultiTenancy;
using ZipZap.BuildingBlocks.Outbox;
using ZipZap.Modules.Catalog.Domain;

namespace ZipZap.Modules.Catalog.Infrastructure;

/// <summary>DbContext modułu Catalog — własny schemat `catalog`.</summary>
public sealed class CatalogDbContext : DbContext, IOutboxDbContext
{
    public const string Schema = "catalog";

    private readonly ICurrentTenant _tenant;

    public CatalogDbContext(DbContextOptions<CatalogDbContext> options, ICurrentTenant tenant)
        : base(options)
        => _tenant = tenant;

    public DbSet<Store> Stores => Set<Store>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.HasDefaultSchema(Schema);

        b.Entity<Store>(e =>
        {
            e.ToTable("stores");
            e.HasKey(s => s.Id);
            e.Property(s => s.Name).IsRequired().HasMaxLength(200);
            e.Property(s => s.Slug).IsRequired().HasMaxLength(200);
            e.HasIndex(s => s.Slug).IsUnique();
            e.Property(s => s.Description);
            e.Property(s => s.City).IsRequired().HasMaxLength(120);
            e.Property(s => s.Address).HasMaxLength(300);
            e.Property(s => s.Phone).HasMaxLength(32);
            e.Property(s => s.CommissionRate).HasColumnType("numeric(5,4)");
            e.Property(s => s.MinimumOrderValue).HasColumnType("numeric(12,2)");
            e.Property(s => s.Status).HasConversion<string>().HasMaxLength(24);
            e.Property(s => s.IsActive);
            e.Property(s => s.CreatedAtUtc);
            e.Ignore(s => s.DomainEvents);
            e.Ignore(s => s.IsAcceptingOrders);
        });

        b.Entity<Category>(e =>
        {
            e.ToTable("categories");
            e.HasKey(c => c.Id);
            e.Property(c => c.Name).IsRequired().HasMaxLength(150);
            e.Property(c => c.SortOrder);
            e.HasIndex(c => new { c.StoreId, c.ParentId, c.SortOrder });
            // Multi-tenancy: filtr po najemcy (pominięty gdy brak kontekstu sklepu).
            e.HasQueryFilter(c => _tenant.StoreId == null || c.StoreId == _tenant.StoreId);
        });

        b.Entity<Product>(e =>
        {
            e.ToTable("products");
            e.HasKey(p => p.Id);
            e.Property(p => p.Name).IsRequired().HasMaxLength(200);
            e.Property(p => p.Description);
            e.Property(p => p.Price).HasColumnType("numeric(12,2)");
            e.Property(p => p.Currency).IsRequired().HasMaxLength(3);
            e.Property(p => p.Unit).IsRequired().HasMaxLength(16);
            e.Property(p => p.ImageUrl).HasMaxLength(500);
            e.Property(p => p.IsAvailable);
            e.Property(p => p.CreatedAtUtc);
            e.HasIndex(p => new { p.StoreId, p.CategoryId });
            e.Ignore(p => p.DomainEvents);
            e.HasQueryFilter(p => _tenant.StoreId == null || p.StoreId == _tenant.StoreId);
        });

        b.Entity<OutboxMessage>(e =>
        {
            e.ToTable("outbox_messages");
            e.HasKey(m => m.Id);
            e.Property(m => m.Type).IsRequired();
            e.Property(m => m.Payload).IsRequired();
            e.HasIndex(m => m.ProcessedAtUtc);
        });

        // Klucze Guid generujemy po stronie klienta (klasa Entity) — wyłączamy ValueGeneratedOnAdd.
        foreach (var key in b.Model.GetEntityTypes().SelectMany(t => t.GetDeclaredKeys()))
            foreach (var prop in key.Properties)
                if (prop.ClrType == typeof(Guid))
                    prop.ValueGenerated = ValueGenerated.Never;
    }
}
