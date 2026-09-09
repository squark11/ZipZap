using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using ZipZap.BuildingBlocks.Outbox;
using ZipZap.Modules.Ordering.Domain;
using ZipZap.Modules.Ordering.Domain.ReadModel;

namespace ZipZap.Modules.Ordering.Infrastructure;

/// <summary>DbContext modułu Ordering — własny schemat `ordering`.</summary>
public sealed class OrderingDbContext : DbContext, IOutboxDbContext
{
    public const string Schema = "ordering";

    public OrderingDbContext(DbContextOptions<OrderingDbContext> options) : base(options) { }

    // Read-model (budowany ze zdarzeń Catalog)
    public DbSet<CatalogStoreView> CatalogStores => Set<CatalogStoreView>();
    public DbSet<CatalogProductView> CatalogProducts => Set<CatalogProductView>();

    public DbSet<Cart> Carts => Set<Cart>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<DeliveryZone> DeliveryZones => Set<DeliveryZone>();
    public DbSet<TimeSlot> TimeSlots => Set<TimeSlot>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.HasDefaultSchema(Schema);

        // ---- Read-model ----
        b.Entity<CatalogStoreView>(e =>
        {
            e.ToTable("catalog_stores");
            e.HasKey(s => s.Id);
            e.Property(s => s.Name).IsRequired().HasMaxLength(200);
            e.Property(s => s.CommissionRate).HasColumnType("numeric(5,4)");
            e.Property(s => s.MinimumOrderValue).HasColumnType("numeric(12,2)");
            e.Property(s => s.Status).HasMaxLength(24);
            e.Ignore(s => s.IsAcceptingOrders);
        });

        b.Entity<CatalogProductView>(e =>
        {
            e.ToTable("catalog_products");
            e.HasKey(p => p.Id);
            e.Property(p => p.Name).IsRequired().HasMaxLength(200);
            e.Property(p => p.Price).HasColumnType("numeric(12,2)");
            e.Property(p => p.Currency).HasMaxLength(3);
            e.Property(p => p.Unit).HasMaxLength(16);
            e.HasIndex(p => p.StoreId);
        });

        // ---- Koszyk ----
        b.Entity<Cart>(e =>
        {
            e.ToTable("carts");
            e.HasKey(c => c.Id);
            e.Property(c => c.CartToken).IsRequired().HasMaxLength(64);
            e.HasIndex(c => c.CartToken).IsUnique(); // token autoryzuje dostęp do koszyka
            e.Property(c => c.Status).HasConversion<string>().HasMaxLength(20);
            e.Ignore(c => c.DomainEvents);
            e.Ignore(c => c.Subtotal);
            e.HasMany(c => c.Items).WithOne().HasForeignKey(i => i.CartId);
            e.Metadata.FindNavigation(nameof(Cart.Items))!.SetPropertyAccessMode(PropertyAccessMode.Field);
        });

        b.Entity<CartItem>(e =>
        {
            e.ToTable("cart_items");
            e.HasKey(i => i.Id);
            e.Property(i => i.ProductName).IsRequired().HasMaxLength(200);
            e.Property(i => i.UnitPrice).HasColumnType("numeric(12,2)");
            e.Ignore(i => i.LineTotal);
        });

        // ---- Zamówienie ----
        b.Entity<Order>(e =>
        {
            e.ToTable("orders");
            e.HasKey(o => o.Id);
            e.Property(o => o.Status).HasConversion<string>().HasMaxLength(20);
            e.Property(o => o.Subtotal).HasColumnType("numeric(12,2)");
            e.Property(o => o.CommissionAmount).HasColumnType("numeric(12,2)");
            e.Property(o => o.DeliveryFee).HasColumnType("numeric(12,2)");
            e.Property(o => o.Total).HasColumnType("numeric(12,2)");
            e.Property(o => o.Currency).HasMaxLength(3);
            e.Property(o => o.DeliveryAddress).IsRequired().HasMaxLength(500);
            e.Property(o => o.ContactPhone).IsRequired().HasMaxLength(32);
            e.Property(o => o.IdempotencyKey).HasMaxLength(80);
            e.HasIndex(o => new { o.StoreId, o.Status });
            e.HasIndex(o => o.CustomerId);
            // Idempotencja checkoutu: para (klient, klucz) unikalna, gdy klucz podany.
            e.HasIndex(o => new { o.CustomerId, o.IdempotencyKey }).IsUnique()
                .HasFilter("\"IdempotencyKey\" IS NOT NULL");
            e.Ignore(o => o.DomainEvents);

            e.HasMany(o => o.Items).WithOne().HasForeignKey(i => i.OrderId);
            e.Metadata.FindNavigation(nameof(Order.Items))!.SetPropertyAccessMode(PropertyAccessMode.Field);
            e.HasMany(o => o.History).WithOne().HasForeignKey(h => h.OrderId);
            e.Metadata.FindNavigation(nameof(Order.History))!.SetPropertyAccessMode(PropertyAccessMode.Field);
        });

        b.Entity<OrderItem>(e =>
        {
            e.ToTable("order_items");
            e.HasKey(i => i.Id);
            e.Property(i => i.ProductName).IsRequired().HasMaxLength(200);
            e.Property(i => i.UnitPrice).HasColumnType("numeric(12,2)");
            e.Ignore(i => i.LineTotal);
        });

        b.Entity<OrderStatusChange>(e =>
        {
            e.ToTable("order_status_history");
            e.HasKey(h => h.Id);
            e.Property(h => h.FromStatus).HasConversion<string>().HasMaxLength(20);
            e.Property(h => h.ToStatus).HasConversion<string>().HasMaxLength(20);
        });

        // ---- Strefy i sloty ----
        b.Entity<DeliveryZone>(e =>
        {
            e.ToTable("delivery_zones");
            e.HasKey(z => z.Id);
            e.Property(z => z.Name).IsRequired().HasMaxLength(150);
            e.Property(z => z.DeliveryFee).HasColumnType("numeric(12,2)");
            e.HasIndex(z => z.StoreId);
            e.Ignore(z => z.PostalCodes); // dopasowanie po kodach — przyszła iteracja
        });

        b.Entity<TimeSlot>(e =>
        {
            e.ToTable("time_slots");
            e.HasKey(s => s.Id);
            e.Property(s => s.MaxOrders);
            e.Property(s => s.ReservedCount);
            e.HasIndex(s => new { s.StoreId, s.DeliveryZoneId, s.Date });
            e.Ignore(s => s.DomainEvents);
            e.Ignore(s => s.HasCapacity);
            e.Ignore(s => s.RemainingCapacity);
            // Ochrona przed przebukowaniem slotu: systemowa kolumna xmin jako token współbieżności.
            // (UseXminAsConcurrencyToken jest oznaczony [Obsolete], ale poprawnie NIE tworzy kolumny
            //  — w przeciwieństwie do IsRowVersion, które kolidowałoby z systemową kolumną xmin.)
#pragma warning disable CS0618
            e.UseXminAsConcurrencyToken();
#pragma warning restore CS0618
        });

        // ---- Outbox ----
        b.Entity<OutboxMessage>(e =>
        {
            e.ToTable("outbox_messages");
            e.HasKey(m => m.Id);
            e.Property(m => m.Type).IsRequired();
            e.Property(m => m.Payload).IsRequired();
            e.HasIndex(m => m.ProcessedAtUtc);
        });

        // Klucze Guid generujemy po stronie klienta (klasa Entity) — wyłączamy
        // ValueGeneratedOnAdd, by EF poprawnie rozpoznawał INSERT dzieci dodanych
        // do już śledzonego agregatu (np. pozycje koszyka).
        foreach (var key in b.Model.GetEntityTypes().SelectMany(t => t.GetDeclaredKeys()))
            foreach (var prop in key.Properties)
                if (prop.ClrType == typeof(Guid))
                    prop.ValueGenerated = ValueGenerated.Never;
    }
}
