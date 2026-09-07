using System.Text;
using Microsoft.EntityFrameworkCore;
using ZipZap.BuildingBlocks.Domain;
using ZipZap.BuildingBlocks.Messaging;
using ZipZap.BuildingBlocks.MultiTenancy;
using ZipZap.BuildingBlocks.Outbox;
using ZipZap.Contracts.Catalog;
using ZipZap.Modules.Catalog.Domain;
using ZipZap.Modules.Catalog.Infrastructure;

namespace ZipZap.Modules.Catalog.Application;

/// <summary>
/// Przypadki użycia Catalog. Odczyty publiczne (przeglądanie bez logowania),
/// zapisy chronione RBAC + kontrolą przynależności do sklepu.
/// </summary>
public sealed class CatalogService
{
    private readonly CatalogDbContext _db;
    private readonly ICurrentUser _currentUser;
    private readonly IIntegrationEventTypeRegistry _events;

    public CatalogService(CatalogDbContext db, ICurrentUser currentUser, IIntegrationEventTypeRegistry events)
    {
        _db = db;
        _currentUser = currentUser;
        _events = events;
    }

    // ---------- Odczyty (publiczne) ----------

    public async Task<IReadOnlyList<StoreDto>> ListStoresAsync(bool onlyActive, CancellationToken ct)
    {
        var query = _db.Stores.AsNoTracking().IgnoreQueryFilters();
        if (onlyActive) query = query.Where(s => s.IsActive);
        return await query.OrderBy(s => s.Name)
            .Select(s => StoreDto.From(s)).ToListAsync(ct);
    }

    public async Task<Result<StoreDto>> GetStoreAsync(string idOrSlug, CancellationToken ct)
    {
        var query = _db.Stores.AsNoTracking().IgnoreQueryFilters();
        Store? store = Guid.TryParse(idOrSlug, out var id)
            ? await query.FirstOrDefaultAsync(s => s.Id == id, ct)
            : await query.FirstOrDefaultAsync(s => s.Slug == idOrSlug, ct);

        return store is null ? Error.NotFound("Sklep nie istnieje.") : StoreDto.From(store);
    }

    public async Task<IReadOnlyList<CategoryDto>> ListCategoriesAsync(Guid storeId, CancellationToken ct)
        => await _db.Categories.AsNoTracking().IgnoreQueryFilters()
            .Where(c => c.StoreId == storeId)
            .OrderBy(c => c.SortOrder).ThenBy(c => c.Name)
            .Select(c => CategoryDto.From(c)).ToListAsync(ct);

    public async Task<IReadOnlyList<ProductDto>> ListProductsAsync(Guid storeId, Guid? categoryId, CancellationToken ct)
    {
        var query = _db.Products.AsNoTracking().IgnoreQueryFilters().Where(p => p.StoreId == storeId);
        if (categoryId.HasValue) query = query.Where(p => p.CategoryId == categoryId.Value);
        return await query.OrderBy(p => p.Name).Select(p => ProductDto.From(p)).ToListAsync(ct);
    }

    public async Task<Result<ProductDto>> GetProductAsync(Guid id, CancellationToken ct)
    {
        var product = await _db.Products.AsNoTracking().IgnoreQueryFilters()
            .FirstOrDefaultAsync(p => p.Id == id, ct);
        return product is null ? Error.NotFound("Produkt nie istnieje.") : ProductDto.From(product);
    }

    // ---------- Zapisy ----------

    public async Task<Result<StoreDto>> CreateStoreAsync(
        string name, string? slug, string? description, string city, string? address,
        decimal commissionRate, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(name)) return Error.Validation("Nazwa sklepu jest wymagana.");
        if (commissionRate is < 0 or > 1) return Error.Validation("Prowizja musi być w zakresie 0–1 (np. 0.10).");

        var finalSlug = Slugify(string.IsNullOrWhiteSpace(slug) ? name : slug!);
        if (await _db.Stores.IgnoreQueryFilters().AnyAsync(s => s.Slug == finalSlug, ct))
            return Error.Conflict($"Sklep o slug '{finalSlug}' już istnieje.");

        var store = Store.Create(name, finalSlug, description, city, address, commissionRate);
        _db.Stores.Add(store);
        _db.AddOutboxMessage(
            new StoreRegistered(store.Id, store.Name, store.Slug, store.CommissionRate, store.City, store.IsActive),
            _events);

        await _db.SaveChangesAsync(ct);
        return StoreDto.From(store);
    }

    public async Task<Result<StoreDto>> UpdateStoreAsync(
        Guid storeId, decimal? commissionRate, bool? isActive, CancellationToken ct)
    {
        var guard = EnsureCanManageStore(storeId);
        if (guard.IsFailure) return guard.Error;

        var store = await _db.Stores.IgnoreQueryFilters().FirstOrDefaultAsync(s => s.Id == storeId, ct);
        if (store is null) return Error.NotFound("Sklep nie istnieje.");

        if (commissionRate.HasValue)
        {
            if (commissionRate is < 0 or > 1) return Error.Validation("Prowizja musi być w zakresie 0–1.");
            store.UpdateCommissionRate(commissionRate.Value);
        }
        if (isActive.HasValue)
        {
            if (isActive.Value) store.Activate(); else store.Deactivate();
        }

        _db.AddOutboxMessage(new StoreUpdated(store.Id, store.CommissionRate, store.IsActive), _events);
        await _db.SaveChangesAsync(ct);
        return StoreDto.From(store);
    }

    public async Task<Result<CategoryDto>> CreateCategoryAsync(
        Guid storeId, string name, int sortOrder, Guid? parentId, CancellationToken ct)
    {
        var guard = EnsureCanManageStore(storeId);
        if (guard.IsFailure) return guard.Error;
        if (string.IsNullOrWhiteSpace(name)) return Error.Validation("Nazwa kategorii jest wymagana.");
        if (!await StoreExists(storeId, ct)) return Error.NotFound("Sklep nie istnieje.");

        var category = new Category(storeId, name, sortOrder, parentId);
        _db.Categories.Add(category);
        await _db.SaveChangesAsync(ct);
        return CategoryDto.From(category);
    }

    public async Task<Result<ProductDto>> CreateProductAsync(
        Guid storeId, Guid? categoryId, string name, string? description,
        decimal price, string? currency, string? unit, int? stockQty, string? imageUrl, CancellationToken ct)
    {
        var guard = EnsureCanManageStore(storeId);
        if (guard.IsFailure) return guard.Error;
        if (string.IsNullOrWhiteSpace(name)) return Error.Validation("Nazwa produktu jest wymagana.");
        if (price < 0) return Error.Validation("Cena nie może być ujemna.");
        if (!await StoreExists(storeId, ct)) return Error.NotFound("Sklep nie istnieje.");

        var product = Product.Create(storeId, categoryId, name, description, price,
            string.IsNullOrWhiteSpace(currency) ? "PLN" : currency!.ToUpperInvariant(),
            string.IsNullOrWhiteSpace(unit) ? "szt" : unit!, stockQty, imageUrl);

        _db.Products.Add(product);
        _db.AddOutboxMessage(
            new ProductPublished(product.Id, product.StoreId, product.Name, product.Price,
                product.Currency, product.Unit, product.IsAvailable),
            _events);

        await _db.SaveChangesAsync(ct);
        return ProductDto.From(product);
    }

    public async Task<Result<ProductDto>> UpdateProductAsync(
        Guid productId, string? name, decimal? price, bool? isAvailable,
        Guid? categoryId, int? stockQty, string? description, string? imageUrl, CancellationToken ct)
    {
        var product = await _db.Products.IgnoreQueryFilters().FirstOrDefaultAsync(p => p.Id == productId, ct);
        if (product is null) return Error.NotFound("Produkt nie istnieje.");

        var guard = EnsureCanManageStore(product.StoreId);
        if (guard.IsFailure) return guard.Error;
        if (price is < 0) return Error.Validation("Cena nie może być ujemna.");

        product.Update(name, price, isAvailable, categoryId, stockQty, description, imageUrl);
        _db.AddOutboxMessage(
            new ProductUpdated(product.Id, product.StoreId, product.Name, product.Price,
                product.Currency, product.Unit, product.IsAvailable),
            _events);

        await _db.SaveChangesAsync(ct);
        return ProductDto.From(product);
    }

    // ---------- Pomocnicze ----------

    private Task<bool> StoreExists(Guid storeId, CancellationToken ct)
        => _db.Stores.IgnoreQueryFilters().AnyAsync(s => s.Id == storeId, ct);

    private Result EnsureCanManageStore(Guid storeId)
    {
        if (_currentUser.Roles.Contains("Admin")) return Result.Success();
        if (_currentUser.Roles.Contains("StoreEmployee") && _currentUser.StoreId == storeId)
            return Result.Success();
        return Result.Failure(Error.Forbidden("Brak uprawnień do zarządzania tym sklepem."));
    }

    private static string Slugify(string input)
    {
        var sb = new StringBuilder(input.Length);
        foreach (var ch in input.Trim().ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(ch)) sb.Append(ch);
            else if (ch is ' ' or '-' or '_' && sb.Length > 0 && sb[^1] != '-') sb.Append('-');
        }
        return sb.ToString().Trim('-') is { Length: > 0 } s ? s : Guid.NewGuid().ToString("n")[..8];
    }
}
