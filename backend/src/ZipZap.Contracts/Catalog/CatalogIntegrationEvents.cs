using ZipZap.BuildingBlocks.Messaging;

namespace ZipZap.Contracts.Catalog;

// Published language modułu Catalog. Wspólny projekt kontraktów pozwala innym
// modułom konsumować te zdarzenia BEZ zależności od wnętrza modułu Catalog.
//
// AggregateVersion = wersja sklepu/produktu w chwili zdarzenia (rośnie z każdą zatwierdzoną zmianą). Zdarzenia mogą
// dotrzeć w innej kolejności (kilka instancji, ponowienie po błędzie, ręczne ponowienie) — konsument stosuje tylko
// wersję nowszą niż już znana. 0 = zdarzenie sprzed wersjonowania (stare wiadomości w outboxie).

public sealed record StoreRegistered(
    Guid StoreId, string Name, string Slug, decimal CommissionRate, string City, bool IsActive,
    string Status, decimal MinimumOrderValue, int AggregateVersion = 0) : IntegrationEvent;

public sealed record StoreUpdated(
    Guid StoreId, decimal CommissionRate, bool IsActive,
    string Status, decimal MinimumOrderValue, int AggregateVersion = 0) : IntegrationEvent;

public sealed record ProductPublished(
    Guid ProductId, Guid StoreId, string Name, decimal Price, string Currency, string Unit, bool IsAvailable,
    string? UnitOptionsJson = null, int AggregateVersion = 0) : IntegrationEvent;

public sealed record ProductUpdated(
    Guid ProductId, Guid StoreId, string Name, decimal Price, string Currency, string Unit, bool IsAvailable,
    string? UnitOptionsJson = null, int AggregateVersion = 0) : IntegrationEvent;

/// <summary>Jedna sprzedażowa jednostka produktu (np. „kg" = 4,99, „szt" = 1,20). Klient wybiera.</summary>
public sealed record ProductUnitOption(string Unit, decimal Price);
