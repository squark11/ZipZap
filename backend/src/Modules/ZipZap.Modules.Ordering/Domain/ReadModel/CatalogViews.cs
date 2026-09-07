namespace ZipZap.Modules.Ordering.Domain.ReadModel;

/// <summary>
/// Lokalny read-model sklepu, budowany ze zdarzeń Catalog (StoreRegistered/StoreUpdated).
/// Daje Ordering autorytatywną stawkę prowizji BEZ sięgania do schematu `catalog`.
/// </summary>
public sealed class CatalogStoreView
{
    public Guid Id { get; set; }              // = StoreId
    public string Name { get; set; } = default!;
    public decimal CommissionRate { get; set; }
    public bool IsActive { get; set; }
}

/// <summary>
/// Lokalny read-model produktu (ProductPublished/ProductUpdated) — autorytatywna
/// nazwa/cena/dostępność w chwili dodania do koszyka i składania zamówienia.
/// </summary>
public sealed class CatalogProductView
{
    public Guid Id { get; set; }              // = ProductId
    public Guid StoreId { get; set; }
    public string Name { get; set; } = default!;
    public decimal Price { get; set; }
    public string Currency { get; set; } = "PLN";
    public string Unit { get; set; } = "szt";
    public bool IsAvailable { get; set; }
}
