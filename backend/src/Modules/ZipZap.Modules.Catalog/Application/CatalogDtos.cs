using ZipZap.Contracts.Catalog;
using ZipZap.Modules.Catalog.Domain;

namespace ZipZap.Modules.Catalog.Application;

public sealed record StoreDto(
    Guid Id, string Name, string Slug, string? Description, string City, string? Address, string? Phone, string? Nip,
    decimal CommissionRate, decimal MinimumOrderValue, bool IsActive, string Status, bool IsAcceptingOrders,
    string? LogoUrl, double? Latitude, double? Longitude, double? DistanceKm = null,
    string? CoverUrl = null, string? DeliveryNote = null, IReadOnlyList<string>? PlatformCategories = null)
{
    /// <summary><paramref name="platformCategories"/>: klucze kategorii platformy, w których sklep ma dostępny produkt
    /// (F-6) — liczone osobnym zapytaniem dla całej listy (CatalogService.WithPlatformCategoriesAsync).</summary>
    public static StoreDto From(Store s, double? distanceKm = null, IReadOnlyList<string>? platformCategories = null) =>
        new(s.Id, s.Name, s.Slug, s.Description, s.City, s.Address, s.Phone, s.Nip,
            s.CommissionRate, s.MinimumOrderValue, s.IsActive, s.Status.ToString(), s.IsAcceptingOrders,
            s.LogoUrl, s.Latitude, s.Longitude, distanceKm, s.CoverUrl, s.DeliveryNote,
            platformCategories ?? Array.Empty<string>());
}

public sealed record CategoryDto(Guid Id, Guid StoreId, string Name, int SortOrder, Guid? ParentId,
    string? PlatformCategoryKey = null)
{
    public static CategoryDto From(Category c) =>
        new(c.Id, c.StoreId, c.Name, c.SortOrder, c.ParentId, c.PlatformCategoryKey);
}

/// <summary>Kategoria platformy (F-6). Klucz = nazwa ikony w aplikacji.</summary>
public sealed record PlatformCategoryDto(string Key, string Name, int SortOrder, bool IsActive)
{
    public static PlatformCategoryDto From(PlatformCategory c) => new(c.Key, c.Name, c.SortOrder, c.IsActive);
}

public sealed record ProductDto(
    Guid Id, Guid StoreId, Guid? CategoryId, string Name, string? Description,
    decimal Price, string Currency, string Unit, int? StockQty, bool IsAvailable, string? ImageUrl,
    IReadOnlyList<ProductUnitOption> UnitOptions)
{
    public static ProductDto From(Product p) =>
        new(p.Id, p.StoreId, p.CategoryId, p.Name, p.Description, p.Price, p.Currency, p.Unit,
            p.StockQty, p.IsAvailable, p.ImageUrl, ParseUnitOptions(p.UnitOptionsJson, p.Unit, p.Price));

    /// <summary>Zawsze zwraca ≥1 opcję: z JSON (gdy jest) albo domyślną (Unit/Price).</summary>
    public static IReadOnlyList<ProductUnitOption> ParseUnitOptions(string? json, string unit, decimal price)
    {
        if (!string.IsNullOrWhiteSpace(json))
        {
            try
            {
                var opts = System.Text.Json.JsonSerializer.Deserialize<List<ProductUnitOption>>(json!);
                if (opts is { Count: > 0 }) return opts;
            }
            catch { /* zły JSON → domyślna */ }
        }
        return new List<ProductUnitOption> { new(unit, price) };
    }
}
