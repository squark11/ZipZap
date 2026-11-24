using ZipZap.Contracts.Catalog;

namespace ZipZap.Modules.Catalog.Api;

public sealed record CreateStoreRequest(
    string Name, string? Slug, string? Description, string City, string? Address, string? Phone,
    decimal CommissionRate, decimal MinimumOrderValue,
    string? LogoUrl = null, double? Latitude = null, double? Longitude = null);

public sealed record UpdateStoreRequest(
    decimal? CommissionRate, bool? IsActive, string? Status, decimal? MinimumOrderValue,
    string? LogoUrl = null, double? Latitude = null, double? Longitude = null,
    string? CoverUrl = null, string? DeliveryNote = null);

public sealed record CreateCategoryRequest(string Name, int SortOrder, Guid? ParentId, string? PlatformCategoryKey = null);

/// <summary>Przypięcie kategorii sklepu do kategorii platformy (F-6); pusty klucz = odepnij.</summary>
public sealed record SetCategoryPlatformRequest(string? Key);

/// <summary>Administrator: zmiana kategorii platformy (null = bez zmian).</summary>
public sealed record UpdatePlatformCategoryRequest(string? Name, int? SortOrder, bool? IsActive);

public sealed record CreateProductRequest(
    Guid? CategoryId, string Name, string? Description, decimal Price,
    string? Currency, string? Unit, int? StockQty, string? ImageUrl,
    IReadOnlyList<ProductUnitOption>? UnitOptions = null);

public sealed record UpdateProductRequest(
    string? Name, decimal? Price, bool? IsAvailable, Guid? CategoryId,
    int? StockQty, string? Description, string? ImageUrl,
    IReadOnlyList<ProductUnitOption>? UnitOptions = null);

/// <summary>Import asortymentu z pliku CSV (surowa treść) + flaga zatwierdzenia.</summary>
public sealed record ImportProductsRequest(string Content);

/// <summary>Wejście na kartę sklepu: źródło z adresu (np. „qr", „qr-kasa"); normalizowane po stronie serwera.</summary>
public sealed record StoreEntryRequest(string? Source);
