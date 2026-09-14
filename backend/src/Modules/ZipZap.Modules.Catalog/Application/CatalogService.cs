using System.Globalization;
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

/// <summary>Wynik jednego wiersza importu: „nowy" / „aktualizacja" / „błąd".</summary>
public sealed record ImportRowResult(int Row, string Name, string Action, string? Error);

/// <summary>Raport importu asortymentu (dry-run lub po zatwierdzeniu).</summary>
public sealed record ImportReport(bool Committed, int Total, int Created, int Updated, int Failed,
    IReadOnlyList<ImportRowResult> Rows);

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
        => await ListStoresAsync(onlyActive, null, null, ct);

    /// <summary>
    /// Lista sklepów. Gdy podano współrzędne klienta (lat/lng), liczy odległość (Haversine)
    /// i sortuje od najbliższego — sklepy bez współrzędnych trafiają na koniec.
    /// </summary>
    public async Task<IReadOnlyList<StoreDto>> ListStoresAsync(bool onlyActive, double? lat, double? lng, CancellationToken ct)
    {
        var query = _db.Stores.AsNoTracking().IgnoreQueryFilters();
        if (onlyActive) query = query.Where(s => s.IsActive);

        var hasCoords = lat is >= -90 and <= 90 && lng is >= -180 and <= 180;
        if (!hasCoords)
        {
            // Brak/nieprawidłowe współrzędne → sortowanie alfabetyczne (zachowanie domyślne).
            return await query.OrderBy(s => s.Name).Select(s => StoreDto.From(s, null)).ToListAsync(ct);
        }

        var stores = await query.ToListAsync(ct);
        return stores
            .Select(s => (store: s, dist: s.Latitude.HasValue && s.Longitude.HasValue
                ? Math.Round(Haversine(lat.Value, lng.Value, s.Latitude.Value, s.Longitude.Value), 1)
                : (double?)null))
            .OrderBy(x => x.dist ?? double.MaxValue).ThenBy(x => x.store.Name)
            .Select(x => StoreDto.From(x.store, x.dist))
            .ToList();
    }

    /// <summary>Odległość po wielkim okręgu (km) między dwoma punktami.</summary>
    private static double Haversine(double lat1, double lon1, double lat2, double lon2)
    {
        const double r = 6371.0; // promień Ziemi w km
        static double Rad(double d) => d * Math.PI / 180.0;
        var dLat = Rad(lat2 - lat1);
        var dLon = Rad(lon2 - lon1);
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2)
                + Math.Cos(Rad(lat1)) * Math.Cos(Rad(lat2)) * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        return r * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
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
        string name, string? slug, string? description, string city, string? address, string? phone,
        decimal commissionRate, decimal minimumOrderValue, CancellationToken ct,
        string? logoUrl = null, double? latitude = null, double? longitude = null)
    {
        if (string.IsNullOrWhiteSpace(name)) return Error.Validation("Nazwa sklepu jest wymagana.");
        if (commissionRate is < 0 or > 1) return Error.Validation("Prowizja musi być w zakresie 0–1 (np. 0.10).");
        if (minimumOrderValue < 0) return Error.Validation("Minimalna wartość zamówienia nie może być ujemna.");

        var finalSlug = Slugify(string.IsNullOrWhiteSpace(slug) ? name : slug!);
        if (await _db.Stores.IgnoreQueryFilters().AnyAsync(s => s.Slug == finalSlug, ct))
            return Error.Conflict($"Sklep o slug '{finalSlug}' już istnieje.");

        var store = Store.Create(name, finalSlug, description, city, address, phone, commissionRate, minimumOrderValue);
        store.SetLogoUrl(logoUrl);
        if (latitude.HasValue && longitude.HasValue)
        {
            var loc = TrySetLocation(store, latitude, longitude);
            if (loc.IsFailure) return loc.Error;
        }
        _db.Stores.Add(store);
        PublishStoreState(store, isRegistration: true);

        await _db.SaveChangesAsync(ct);
        return StoreDto.From(store);
    }

    private static Result TrySetLocation(Store store, double? latitude, double? longitude)
    {
        try { store.SetLocation(latitude, longitude); return Result.Success(); }
        catch (ArgumentOutOfRangeException ex) { return Result.Failure(Error.Validation(ex.Message)); }
    }

    public async Task<Result<StoreDto>> UpdateStoreAsync(
        Guid storeId, decimal? commissionRate, bool? isActive, string? status, decimal? minimumOrderValue, CancellationToken ct,
        string? logoUrl = null, double? latitude = null, double? longitude = null)
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
        if (minimumOrderValue.HasValue)
        {
            if (minimumOrderValue < 0) return Error.Validation("Minimalna wartość zamówienia nie może być ujemna.");
            store.UpdateMinimumOrderValue(minimumOrderValue.Value);
        }
        if (isActive.HasValue)
        {
            if (isActive.Value) store.Activate(); else store.Deactivate();
        }
        if (!string.IsNullOrWhiteSpace(status))
        {
            if (!Enum.TryParse<StoreStatus>(status, ignoreCase: true, out var parsed))
                return Error.Validation($"Nieznany status '{status}' (Open/Closed/TemporarilyUnavailable).");
            store.SetStatus(parsed);
        }
        // logoUrl != null => ustaw (pusty łańcuch = wyczyść); współrzędne ustawiamy parami.
        if (logoUrl is not null) store.SetLogoUrl(logoUrl);
        if (latitude.HasValue && longitude.HasValue)
        {
            var loc = TrySetLocation(store, latitude, longitude);
            if (loc.IsFailure) return loc.Error;
        }

        PublishStoreState(store, isRegistration: false);
        await _db.SaveChangesAsync(ct);
        return StoreDto.From(store);
    }

    /// <summary>
    /// Re-emituje StoreUpdated dla wszystkich sklepów, odświeżając read-modele
    /// konsumentów (np. Ordering). Naprawia rekordy sprzed rozszerzenia zdarzeń
    /// o Status/MinimumOrderValue (stary sklep z pustym Status = nie przyjmuje
    /// zamówień). Tylko admin. Idempotentne.
    /// </summary>
    public async Task<Result<int>> ResyncStoreProjectionsAsync(CancellationToken ct)
    {
        var stores = await _db.Stores.AsNoTracking().IgnoreQueryFilters().ToListAsync(ct);
        foreach (var store in stores)
            PublishStoreState(store, isRegistration: false);
        await _db.SaveChangesAsync(ct);
        return stores.Count;
    }

    private void PublishStoreState(Store store, bool isRegistration)
    {
        if (isRegistration)
            _db.AddOutboxMessage(new StoreRegistered(store.Id, store.Name, store.Slug, store.CommissionRate,
                store.City, store.IsActive, store.Status.ToString(), store.MinimumOrderValue), _events);
        else
            _db.AddOutboxMessage(new StoreUpdated(store.Id, store.CommissionRate, store.IsActive,
                store.Status.ToString(), store.MinimumOrderValue), _events);
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

    /// <summary>
    /// Import asortymentu z CSV (separator „;", nagłówek: nazwa;kategoria;cena;jednostka;dostepny).
    /// Upsert po nazwie w obrębie sklepu; kategorie dopasowane po nazwie (tworzone gdy brak).
    /// `commit=false` = tylko walidacja/podgląd (bez zapisu).
    /// </summary>
    public async Task<Result<ImportReport>> ImportProductsAsync(Guid storeId, string content, bool commit, CancellationToken ct)
    {
        var guard = EnsureCanManageStore(storeId);
        if (guard.IsFailure) return guard.Error;
        if (!await StoreExists(storeId, ct)) return Error.NotFound("Sklep nie istnieje.");
        if (string.IsNullOrWhiteSpace(content)) return Error.Validation("Pusty plik.");

        var lines = content.TrimStart('﻿').Replace("\r\n", "\n").Replace("\r", "\n")
            .Split('\n').Where(l => l.Trim().Length > 0).ToList();
        if (lines.Count < 2) return Error.Validation("Brak wierszy danych (nagłówek + min. 1 wiersz).");

        var header = lines[0].Split(';').Select(h => h.Trim().ToLowerInvariant()).ToList();
        int iName = header.IndexOf("nazwa"), iCat = header.IndexOf("kategoria"),
            iPrice = header.IndexOf("cena"), iUnit = header.IndexOf("jednostka"), iAvail = header.IndexOf("dostepny");
        if (iName < 0 || iPrice < 0 || iUnit < 0)
            return Error.Validation("Nagłówek musi zawierać kolumny: nazwa;kategoria;cena;jednostka;dostepny.");

        var existingCats = await _db.Categories.IgnoreQueryFilters().Where(c => c.StoreId == storeId).ToListAsync(ct);
        var catByName = existingCats.ToDictionary(c => c.Name.Trim().ToLowerInvariant(), c => c);
        var newCats = new Dictionary<string, Category>();
        var existingProducts = await _db.Products.IgnoreQueryFilters().Where(p => p.StoreId == storeId).ToListAsync(ct);
        var prodByName = existingProducts
            .GroupBy(p => p.Name.Trim().ToLowerInvariant())
            .ToDictionary(g => g.Key, g => g.First());

        var results = new List<ImportRowResult>();
        var seen = new HashSet<string>();
        int created = 0, updated = 0, failed = 0;

        for (var r = 1; r < lines.Count; r++)
        {
            var cols = lines[r].Split(';');
            string cell(int i) => i >= 0 && i < cols.Length ? cols[i].Trim() : "";
            var name = cell(iName);
            var rowNo = r + 1;

            if (string.IsNullOrWhiteSpace(name))
            { results.Add(new(rowNo, name, "błąd", "brak nazwy")); failed++; continue; }

            var key = name.ToLowerInvariant();
            if (!seen.Add(key))
            { results.Add(new(rowNo, name, "błąd", "duplikat nazwy w pliku")); failed++; continue; }

            var priceStr = cell(iPrice).Replace(" ", "").Replace(",", ".");
            if (!decimal.TryParse(priceStr, NumberStyles.Number, CultureInfo.InvariantCulture, out var price) || price < 0)
            { results.Add(new(rowNo, name, "błąd", $"nieprawidłowa cena '{cell(iPrice)}'")); failed++; continue; }

            var unit = cell(iUnit);
            if (string.IsNullOrWhiteSpace(unit)) unit = "szt";
            var availCell = (iAvail >= 0 ? cell(iAvail) : "tak").ToLowerInvariant();
            var isAvailable = availCell is "" or "tak" or "true" or "1" or "yes" or "y";

            Category? category = null;
            var catName = iCat >= 0 ? cell(iCat) : "";
            if (!string.IsNullOrWhiteSpace(catName))
            {
                var ckey = catName.ToLowerInvariant();
                if (catByName.TryGetValue(ckey, out var ec)) category = ec;
                else if (newCats.TryGetValue(ckey, out var nc)) category = nc;
                else
                {
                    category = new Category(storeId, catName, existingCats.Count + newCats.Count, null);
                    newCats[ckey] = category;
                    if (commit) _db.Categories.Add(category);
                }
            }

            if (prodByName.TryGetValue(key, out var existing))
            {
                if (commit)
                {
                    existing.Update(name, price, isAvailable, category?.Id, null, null, null);
                    _db.AddOutboxMessage(new ProductUpdated(existing.Id, existing.StoreId, existing.Name,
                        existing.Price, existing.Currency, existing.Unit, existing.IsAvailable), _events);
                }
                results.Add(new(rowNo, name, "aktualizacja", null)); updated++;
            }
            else
            {
                if (commit)
                {
                    var product = Product.Create(storeId, category?.Id, name, null, price, "PLN", unit, null, null);
                    if (!isAvailable) product.Update(null, null, false, null, null, null, null);
                    _db.Products.Add(product);
                    _db.AddOutboxMessage(new ProductPublished(product.Id, product.StoreId, product.Name,
                        product.Price, product.Currency, product.Unit, product.IsAvailable), _events);
                }
                results.Add(new(rowNo, name, "nowy", null)); created++;
            }
        }

        if (commit) await _db.SaveChangesAsync(ct);
        return new ImportReport(commit, results.Count, created, updated, failed, results);
    }

    /// <summary>
    /// Eksport asortymentu do CSV w formacie zgodnym z importem
    /// (nazwa;kategoria;cena;jednostka;dostepny) — do edycji offline i ponownego wgrania.
    /// Wiersze z separatorem/cudzysłowem są cytowane (RFC 4180).
    /// </summary>
    public async Task<Result<string>> ExportProductsAsync(Guid storeId, CancellationToken ct)
    {
        var guard = EnsureCanManageStore(storeId);
        if (guard.IsFailure) return guard.Error;
        if (!await StoreExists(storeId, ct)) return Error.NotFound("Sklep nie istnieje.");

        var cats = await _db.Categories.AsNoTracking().IgnoreQueryFilters()
            .Where(c => c.StoreId == storeId).ToDictionaryAsync(c => c.Id, c => c.Name, ct);
        var products = await _db.Products.AsNoTracking().IgnoreQueryFilters()
            .Where(p => p.StoreId == storeId).ToListAsync(ct);

        string CatName(Guid? id) => id.HasValue && cats.TryGetValue(id.Value, out var n) ? n : "";

        var ordered = products
            .OrderBy(p => CatName(p.CategoryId), StringComparer.OrdinalIgnoreCase)
            .ThenBy(p => p.Name, StringComparer.OrdinalIgnoreCase);

        var sb = new StringBuilder();
        sb.Append("nazwa;kategoria;cena;jednostka;dostepny\r\n");
        foreach (var p in ordered)
        {
            var price = p.Price.ToString("0.00", CultureInfo.InvariantCulture).Replace('.', ',');
            sb.Append(Csv(p.Name)).Append(';')
              .Append(Csv(CatName(p.CategoryId))).Append(';')
              .Append(price).Append(';')
              .Append(Csv(p.Unit)).Append(';')
              .Append(p.IsAvailable ? "tak" : "nie").Append("\r\n");
        }
        return sb.ToString();

        static string Csv(string? field)
        {
            field ??= "";
            return field.IndexOfAny(new[] { ';', '"', '\n', '\r' }) >= 0
                ? "\"" + field.Replace("\"", "\"\"") + "\""
                : field;
        }
    }

    // ---------- Pomocnicze ----------

    private Task<bool> StoreExists(Guid storeId, CancellationToken ct)
        => _db.Stores.IgnoreQueryFilters().AnyAsync(s => s.Id == storeId, ct);

    private Result EnsureCanManageStore(Guid storeId)
        => _currentUser.ManagesStore(storeId)
            ? Result.Success()
            : Result.Failure(Error.Forbidden("Brak uprawnień do zarządzania tym sklepem."));

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
