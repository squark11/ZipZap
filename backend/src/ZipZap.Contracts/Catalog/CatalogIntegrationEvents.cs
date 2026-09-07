using ZipZap.BuildingBlocks.Messaging;

namespace ZipZap.Contracts.Catalog;

// Published language modułu Catalog. Wspólny projekt kontraktów pozwala innym
// modułom konsumować te zdarzenia BEZ zależności od wnętrza modułu Catalog.

public sealed record StoreRegistered(
    Guid StoreId, string Name, string Slug, decimal CommissionRate, string City, bool IsActive) : IntegrationEvent;

public sealed record StoreUpdated(
    Guid StoreId, decimal CommissionRate, bool IsActive) : IntegrationEvent;

public sealed record ProductPublished(
    Guid ProductId, Guid StoreId, string Name, decimal Price, string Currency, string Unit, bool IsAvailable) : IntegrationEvent;

public sealed record ProductUpdated(
    Guid ProductId, Guid StoreId, string Name, decimal Price, string Currency, string Unit, bool IsAvailable) : IntegrationEvent;
