using ZipZap.Modules.Catalog.Domain;

namespace ZipZap.Modules.Catalog.Application;

public sealed record StoreDto(
    Guid Id, string Name, string Slug, string? Description, string City, string? Address, string? Phone, string? Nip,
    decimal CommissionRate, decimal MinimumOrderValue, bool IsActive, string Status, bool IsAcceptingOrders,
    string? LogoUrl, double? Latitude, double? Longitude, double? DistanceKm = null)
{
    public static StoreDto From(Store s, double? distanceKm = null) =>
        new(s.Id, s.Name, s.Slug, s.Description, s.City, s.Address, s.Phone, s.Nip,
            s.CommissionRate, s.MinimumOrderValue, s.IsActive, s.Status.ToString(), s.IsAcceptingOrders,
            s.LogoUrl, s.Latitude, s.Longitude, distanceKm);
}

public sealed record CategoryDto(Guid Id, Guid StoreId, string Name, int SortOrder, Guid? ParentId)
{
    public static CategoryDto From(Category c) => new(c.Id, c.StoreId, c.Name, c.SortOrder, c.ParentId);
}

public sealed record ProductDto(
    Guid Id, Guid StoreId, Guid? CategoryId, string Name, string? Description,
    decimal Price, string Currency, string Unit, int? StockQty, bool IsAvailable, string? ImageUrl)
{
    public static ProductDto From(Product p) =>
        new(p.Id, p.StoreId, p.CategoryId, p.Name, p.Description, p.Price, p.Currency, p.Unit,
            p.StockQty, p.IsAvailable, p.ImageUrl);
}
