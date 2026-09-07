using ZipZap.BuildingBlocks.Domain;

namespace ZipZap.Modules.Catalog.Domain;

/// <summary>Produkt sklepu (multi-tenant: StoreId).</summary>
public sealed class Product : AggregateRoot
{
    public Guid StoreId { get; private set; }
    public Guid? CategoryId { get; private set; }
    public string Name { get; private set; } = default!;
    public string? Description { get; private set; }
    public decimal Price { get; private set; }
    public string Currency { get; private set; } = "PLN";
    public string Unit { get; private set; } = "szt";
    public int? StockQty { get; private set; }
    public bool IsAvailable { get; private set; }
    public string? ImageUrl { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    private Product() { } // EF

    private Product(Guid id, Guid storeId, Guid? categoryId, string name, string? description,
        decimal price, string currency, string unit, int? stockQty, string? imageUrl)
    {
        Id = id;
        StoreId = storeId;
        CategoryId = categoryId;
        Name = name;
        Description = description;
        Price = price;
        Currency = currency;
        Unit = unit;
        StockQty = stockQty;
        ImageUrl = imageUrl;
        IsAvailable = true;
        CreatedAtUtc = DateTime.UtcNow;
    }

    public static Product Create(Guid storeId, Guid? categoryId, string name, string? description,
        decimal price, string currency, string unit, int? stockQty, string? imageUrl)
        => new(Guid.NewGuid(), storeId, categoryId, name.Trim(), description,
               price, currency, unit, stockQty, imageUrl);

    public void Update(string? name, decimal? price, bool? isAvailable,
        Guid? categoryId, int? stockQty, string? description, string? imageUrl)
    {
        if (name is not null) Name = name.Trim();
        if (price.HasValue) Price = price.Value;
        if (isAvailable.HasValue) IsAvailable = isAvailable.Value;
        if (categoryId.HasValue) CategoryId = categoryId.Value;
        if (stockQty.HasValue) StockQty = stockQty.Value;
        if (description is not null) Description = description;
        if (imageUrl is not null) ImageUrl = imageUrl;
    }
}
