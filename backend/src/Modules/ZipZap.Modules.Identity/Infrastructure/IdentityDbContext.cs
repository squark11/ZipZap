using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using ZipZap.BuildingBlocks.Outbox;
using ZipZap.Modules.Identity.Domain;

namespace ZipZap.Modules.Identity.Infrastructure;

/// <summary>DbContext modułu Identity — własny schemat `identity`.</summary>
public sealed class IdentityDbContext : DbContext, IOutboxDbContext
{
    public const string Schema = "identity";

    public IdentityDbContext(DbContextOptions<IdentityDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<UserToken> UserTokens => Set<UserToken>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.HasDefaultSchema(Schema);

        b.Entity<User>(e =>
        {
            e.ToTable("users");
            e.HasKey(u => u.Id);
            e.Property(u => u.Email).IsRequired().HasMaxLength(256);
            e.HasIndex(u => u.Email).IsUnique();
            e.Property(u => u.PasswordHash).IsRequired();
            e.Property(u => u.FullName).IsRequired().HasMaxLength(200);
            e.Property(u => u.Phone).HasMaxLength(32);
            e.Property(u => u.IsActive);
            e.Property(u => u.IsEmailVerified);
            e.Property(u => u.CreatedAtUtc);
            e.Ignore(u => u.DomainEvents);

            e.HasMany(u => u.Roles).WithOne().HasForeignKey(r => r.UserId);
            e.Metadata.FindNavigation(nameof(User.Roles))!
                .SetPropertyAccessMode(PropertyAccessMode.Field);
        });

        b.Entity<UserRole>(e =>
        {
            e.ToTable("user_roles");
            e.HasKey(r => r.Id);
            e.Property(r => r.Role).HasConversion<string>().HasMaxLength(32).IsRequired();
            e.HasIndex(r => new { r.UserId, r.Role, r.StoreId }).IsUnique();
        });

        b.Entity<RefreshToken>(e =>
        {
            e.ToTable("refresh_tokens");
            e.HasKey(t => t.Id);
            e.Property(t => t.TokenHash).IsRequired().HasMaxLength(128);
            e.HasIndex(t => t.TokenHash);
            e.HasOne<User>().WithMany().HasForeignKey(t => t.UserId);
        });

        b.Entity<UserToken>(e =>
        {
            e.ToTable("user_tokens");
            e.HasKey(t => t.Id);
            e.Property(t => t.Type).HasConversion<string>().HasMaxLength(32).IsRequired();
            e.Property(t => t.TokenHash).IsRequired().HasMaxLength(128);
            e.HasIndex(t => t.TokenHash);
            e.HasIndex(t => new { t.UserId, t.Type });
            e.HasOne<User>().WithMany().HasForeignKey(t => t.UserId);
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
