using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using ZipZap.Modules.Delivery.Domain;

namespace ZipZap.Modules.Delivery.Infrastructure;

public sealed class DeliveryDbContext : DbContext
{
    public const string Schema = "delivery";

    public DeliveryDbContext(DbContextOptions<DeliveryDbContext> options) : base(options) { }

    public DbSet<Domain.Delivery> Deliveries => Set<Domain.Delivery>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.HasDefaultSchema(Schema);

        b.Entity<Domain.Delivery>(e =>
        {
            e.ToTable("deliveries");
            e.HasKey(d => d.Id);
            e.HasIndex(d => d.OrderId).IsUnique();
            e.HasIndex(d => new { d.StoreId, d.Status });
            e.Property(d => d.Status).HasConversion<string>().HasMaxLength(24);
        });

        foreach (var key in b.Model.GetEntityTypes().SelectMany(t => t.GetDeclaredKeys()))
            foreach (var prop in key.Properties)
                if (prop.ClrType == typeof(Guid))
                    prop.ValueGenerated = ValueGenerated.Never;
    }
}
