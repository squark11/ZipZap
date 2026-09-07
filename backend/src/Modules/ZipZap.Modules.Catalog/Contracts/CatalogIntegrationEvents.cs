using ZipZap.BuildingBlocks.Messaging;

namespace ZipZap.Modules.Catalog.Contracts;

/// <summary>
/// PUBLICZNE kontrakty Catalog. Inne moduły (np. Ordering) budują z nich
/// własny read-model — bez synchronicznego odpytywania Catalog ani wspólnych tabel.
/// </summary>
public sealed record StoreRegistered(
    Guid StoreId, string Name, string Slug, decimal CommissionRate, string City, bool IsActive) : IntegrationEvent;

public sealed record StoreUpdated(
    Guid StoreId, decimal CommissionRate, bool IsActive) : IntegrationEvent;

public sealed record ProductPublished(
    Guid ProductId, Guid StoreId, string Name, decimal Price, string Currency, string Unit, bool IsAvailable) : IntegrationEvent;

public sealed record ProductUpdated(
    Guid ProductId, Guid StoreId, string Name, decimal Price, string Currency, string Unit, bool IsAvailable) : IntegrationEvent;
