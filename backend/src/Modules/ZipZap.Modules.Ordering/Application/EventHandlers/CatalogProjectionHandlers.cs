using Microsoft.EntityFrameworkCore;
using ZipZap.BuildingBlocks.Messaging;
using ZipZap.Contracts.Catalog;
using ZipZap.Modules.Ordering.Domain.ReadModel;
using ZipZap.Modules.Ordering.Infrastructure;

namespace ZipZap.Modules.Ordering.Application.EventHandlers;

/// <summary>
/// Buduje lokalny read-model sklepów ze zdarzeń Catalog. Idempotentny upsert
/// (dostawa at-least-once z outboxa).
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
                IsActive = e.IsActive,
            });
        }
        else
        {
            view.Name = e.Name;
            view.CommissionRate = e.CommissionRate;
            view.IsActive = e.IsActive;
        }
        await _db.SaveChangesAsync(ct);
    }

    public async Task HandleAsync(StoreUpdated e, CancellationToken ct = default)
    {
        var view = await _db.CatalogStores.FirstOrDefaultAsync(s => s.Id == e.StoreId, ct);
        if (view is null) return;
        view.CommissionRate = e.CommissionRate;
        view.IsActive = e.IsActive;
        await _db.SaveChangesAsync(ct);
    }
}

/// <summary>Buduje lokalny read-model produktów ze zdarzeń Catalog.</summary>
public sealed class CatalogProductProjectionHandler :
    IIntegrationEventHandler<ProductPublished>,
    IIntegrationEventHandler<ProductUpdated>
{
    private readonly OrderingDbContext _db;

    public CatalogProductProjectionHandler(OrderingDbContext db) => _db = db;

    public Task HandleAsync(ProductPublished e, CancellationToken ct = default)
        => UpsertAsync(e.ProductId, e.StoreId, e.Name, e.Price, e.Currency, e.Unit, e.IsAvailable, ct);

    public Task HandleAsync(ProductUpdated e, CancellationToken ct = default)
        => UpsertAsync(e.ProductId, e.StoreId, e.Name, e.Price, e.Currency, e.Unit, e.IsAvailable, ct);

    private async Task UpsertAsync(Guid productId, Guid storeId, string name, decimal price,
        string currency, string unit, bool isAvailable, CancellationToken ct)
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
                IsAvailable = isAvailable,
            });
        }
        else
        {
            view.StoreId = storeId;
            view.Name = name;
            view.Price = price;
            view.Currency = currency;
            view.Unit = unit;
            view.IsAvailable = isAvailable;
        }
        await _db.SaveChangesAsync(ct);
    }
}
