namespace ZipZap.Modules.Catalog.Api;

public sealed record CreateStoreRequest(
    string Name, string? Slug, string? Description, string City, string? Address, string? Phone,
    decimal CommissionRate, decimal MinimumOrderValue);

public sealed record UpdateStoreRequest(
    decimal? CommissionRate, bool? IsActive, string? Status, decimal? MinimumOrderValue);

public sealed record CreateCategoryRequest(string Name, int SortOrder, Guid? ParentId);

public sealed record CreateProductRequest(
    Guid? CategoryId, string Name, string? Description, decimal Price,
    string? Currency, string? Unit, int? StockQty, string? ImageUrl);

public sealed record UpdateProductRequest(
    string? Name, decimal? Price, bool? IsAvailable, Guid? CategoryId,
    int? StockQty, string? Description, string? ImageUrl);
