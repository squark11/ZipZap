using ZipZap.BuildingBlocks.Domain;

namespace ZipZap.Modules.Catalog.Domain;

/// <summary>Status operacyjny sklepu (czy przyjmuje zamówienia).</summary>
public enum StoreStatus { Open, Closed, TemporarilyUnavailable }

/// <summary>Sklep — korzeń najemcy (StoreId = <see cref="Entity.Id"/>).</summary>
public sealed class Store : AggregateRoot
{
    public string Name { get; private set; } = default!;
    public string Slug { get; private set; } = default!;
    public string? Description { get; private set; }
    public string City { get; private set; } = default!;
    public string? Address { get; private set; }
    public string? Phone { get; private set; }

    /// <summary>Prowizja ZipZap od wartości koszyka (0.10 = 10%).</summary>
    public decimal CommissionRate { get; private set; }

    /// <summary>Minimalna wartość koszyka (produktów), by złożyć zamówienie.</summary>
    public decimal MinimumOrderValue { get; private set; }

    /// <summary>Administracyjne włączenie sklepu (widoczność na platformie).</summary>
    public bool IsActive { get; private set; }

    /// <summary>Status operacyjny (przyjmowanie zamówień).</summary>
    public StoreStatus Status { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    /// <summary>Sklep przyjmuje zamówienia tylko gdy aktywny i otwarty.</summary>
    public bool IsAcceptingOrders => IsActive && Status == StoreStatus.Open;

    private Store() { } // EF

    private Store(Guid id, string name, string slug, string? description,
        string city, string? address, string? phone, decimal commissionRate, decimal minimumOrderValue)
    {
        Id = id;
        Name = name;
        Slug = slug;
        Description = description;
        City = city;
        Address = address;
        Phone = phone;
        CommissionRate = commissionRate;
        MinimumOrderValue = minimumOrderValue;
        IsActive = true;
        Status = StoreStatus.Open;
        CreatedAtUtc = DateTime.UtcNow;
    }

    public static Store Create(string name, string slug, string? description,
        string city, string? address, string? phone, decimal commissionRate, decimal minimumOrderValue)
        => new(Guid.NewGuid(), name.Trim(), slug, description, city.Trim(), address, phone, commissionRate, minimumOrderValue);

    public void UpdateCommissionRate(decimal rate) => CommissionRate = rate;
    public void UpdateMinimumOrderValue(decimal value) => MinimumOrderValue = value;
    public void SetStatus(StoreStatus status) => Status = status;
    public void Activate() => IsActive = true;
    public void Deactivate() => IsActive = false;
}
