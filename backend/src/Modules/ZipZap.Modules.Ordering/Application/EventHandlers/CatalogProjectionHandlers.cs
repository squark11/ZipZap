using Microsoft.EntityFrameworkCore;
using ZipZap.BuildingBlocks.Messaging;
using ZipZap.Contracts.Catalog;
using ZipZap.Modules.Ordering.Domain.ReadModel;
using ZipZap.Modules.Ordering.Infrastructure;

namespace ZipZap.Modules.Ordering.Application.EventHandlers;

/// <summary>
/// Buduje lokalny read-model sklepów ze zdarzeń Catalog. Idempotentny i odporny na kolejność: stosuje tylko wersję
/// sklepu nowszą od znanej (<see cref="ProjectionVersion"/>) — starsze zdarzenie, które skończyło się później
/// (inna instancja, ponowienie), nie cofa nowszego stanu.
/// </summary>
public sealed class CatalogStoreProjectionHandler :
    IIntegrationEventHandler<StoreRegistered>,
    IIntegrationEventHandler<StoreUpdated>
{
    private readonly OrderingDbContext _db;

    public CatalogStoreProjectionHandler(OrderingDbContext db) => _db = db;

    public async Task HandleAsync(StoreRegistered e, CancellationToken ct = default)
    {
        var view = await _db.CatalogStores.FirstOrDefaultAsync(s => s.Id == e.StoreId, ct);
        if (view is null)
        {
            _db.CatalogStores.Add(new CatalogStoreView
            {
                Id = e.StoreId,
                Name = e.Name,
                CommissionRate = e.CommissionRate,
                MinimumOrderValue = e.MinimumOrderValue,
                IsActive = e.IsActive,
                Status = e.Status,
                SourceVersion = e.AggregateVersion,
            });
        }
        else
        {
            // Nazwę niesie tylko StoreRegistered — uzupełnij ją, gdy nowszy StoreUpdated dotarł wcześniej.
            if (string.IsNullOrEmpty(view.Name)) view.Name = e.Name;
            if (ProjectionVersion.IsNewer(e.AggregateVersion, view.SourceVersion))
            {
                view.Name = e.Name;
                view.CommissionRate = e.CommissionRate;
                view.MinimumOrderValue = e.MinimumOrderValue;
                view.IsActive = e.IsActive;
                view.Status = e.Status;
                view.SourceVersion = e.AggregateVersion;
            }
        }
        await _db.SaveChangesAsync(ct);
    }

    public async Task HandleAsync(StoreUpdated e, CancellationToken ct = default)
    {
        var view = await _db.CatalogStores.FirstOrDefaultAsync(s => s.Id == e.StoreId, ct);
        if (view is null)
        {
            // Nowszy stan dotarł przed rejestracją — zapisz go; nazwę uzupełni (starszy) StoreRegistered.
            _db.CatalogStores.Add(new CatalogStoreView
            {
                Id = e.StoreId,
                Name = string.Empty,
                CommissionRate = e.CommissionRate,
                MinimumOrderValue = e.MinimumOrderValue,
                IsActive = e.IsActive,
                Status = e.Status,
                SourceVersion = e.AggregateVersion,
            });
        }
        else if (ProjectionVersion.IsNewer(e.AggregateVersion, view.SourceVersion))
        {
            view.CommissionRate = e.CommissionRate;
            view.MinimumOrderValue = e.MinimumOrderValue;
            view.IsActive = e.IsActive;
            view.Status = e.Status;
            view.SourceVersion = e.AggregateVersion;
        }
        else
        {
            return; // starsza lub ta sama wersja — już zastosowano nowszy stan
        }
        await _db.SaveChangesAsync(ct);
    }
}

/// <summary>Buduje lokalny read-model produktów ze zdarzeń Catalog (tylko wersje nowsze od znanej).</summary>
public sealed class CatalogProductProjectionHandler :
    IIntegrationEventHandler<ProductPublished>,
    IIntegrationEventHandler<ProductUpdated>
{
    private readonly OrderingDbContext _db;

    public CatalogProductProjectionHandler(OrderingDbContext db) => _db = db;

    public Task HandleAsync(ProductPublished e, CancellationToken ct = default)
        => UpsertAsync(e.ProductId, e.StoreId, e.Name, e.Price, e.Currency, e.Unit, e.IsAvailable, e.UnitOptionsJson,
            e.AggregateVersion, ct);

    public Task HandleAsync(ProductUpdated e, CancellationToken ct = default)
        => UpsertAsync(e.ProductId, e.StoreId, e.Name, e.Price, e.Currency, e.Unit, e.IsAvailable, e.UnitOptionsJson,
            e.AggregateVersion, ct);

    private async Task UpsertAsync(Guid productId, Guid storeId, string name, decimal price,
        string currency, string unit, bool isAvailable, string? unitOptionsJson, int version, CancellationToken ct)
    {
        var view = await _db.CatalogProducts.FirstOrDefaultAsync(p => p.Id == productId, ct);
        if (view is null)
        {
            _db.CatalogProducts.Add(new CatalogProductView
            {
                Id = productId,
                StoreId = storeId,
                Name = name,
                Price = price,
                Currency = currency,
                Unit = unit,
                UnitOptionsJson = unitOptionsJson,
                IsAvailable = isAvailable,
                SourceVersion = version,
            });
        }
        else if (ProjectionVersion.IsNewer(version, view.SourceVersion))
        {
            view.StoreId = storeId;
            view.Name = name;
            view.Price = price;
            view.Currency = currency;
            view.Unit = unit;
            view.UnitOptionsJson = unitOptionsJson;
            view.IsAvailable = isAvailable;
            view.SourceVersion = version;
        }
        else
        {
            return; // starsza lub ta sama wersja — już zastosowano nowszy stan
        }
        await _db.SaveChangesAsync(ct);
    }
}
