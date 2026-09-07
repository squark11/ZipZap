using ZipZap.BuildingBlocks.Domain;

namespace ZipZap.Modules.Catalog.Domain;

/// <summary>Sklep — korzeń najemcy (StoreId = <see cref="Entity.Id"/>).</summary>
public sealed class Store : AggregateRoot
{
    public string Name { get; private set; } = default!;
    public string Slug { get; private set; } = default!;
    public string? Description { get; private set; }
    public string City { get; private set; } = default!;
    public string? Address { get; private set; }

    /// <summary>Prowizja ZipZap od wartości koszyka (0.10 = 10%).</summary>
    public decimal CommissionRate { get; private set; }

    public bool IsActive { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    private Store() { } // EF

    private Store(Guid id, string name, string slug, string? description,
        string city, string? address, decimal commissionRate)
    {
        Id = id;
        Name = name;
        Slug = slug;
        Description = description;
        City = city;
        Address = address;
        CommissionRate = commissionRate;
        IsActive = true;
        CreatedAtUtc = DateTime.UtcNow;
    }

    public static Store Create(string name, string slug, string? description,
        string city, string? address, decimal commissionRate)
        => new(Guid.NewGuid(), name.Trim(), slug, description, city.Trim(), address, commissionRate);

    public void UpdateCommissionRate(decimal rate) => CommissionRate = rate;
    public void Activate() => IsActive = true;
    public void Deactivate() => IsActive = false;
}
