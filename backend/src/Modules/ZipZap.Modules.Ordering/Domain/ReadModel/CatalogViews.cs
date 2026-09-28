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
    public decimal MinimumOrderValue { get; set; }
    public bool IsActive { get; set; }
    public string Status { get; set; } = "Open";

    /// <summary>Wersja sklepu z ostatnio zastosowanego zdarzenia (<see cref="ProjectionVersion"/>).</summary>
    public int SourceVersion { get; set; }

    public bool IsAcceptingOrders => IsActive && Status == "Open";
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
    /// <summary>Opcje jednostek (JSON: [{"unit","price"}]). Null = jedna jednostka (Unit/Price).</summary>
    public string? UnitOptionsJson { get; set; }
    public bool IsAvailable { get; set; }

    /// <summary>Wersja produktu z ostatnio zastosowanego zdarzenia (<see cref="ProjectionVersion"/>).</summary>
    public int SourceVersion { get; set; }
}

/// <summary>
/// Monotoniczna wersja projekcji: zdarzenia Catalog mogą dotrzeć w innej kolejności (kilka instancji API, ponowienie
/// po błędzie przejściowym, ręczne ponowienie z panelu), więc stosujemy tylko wersję NOWSZĄ od znanej — starsze
/// i powtórzone są pomijane. Nic nie jest blokowane: zdarzenia innych sklepów/produktów idą niezależnie.
/// </summary>
public static class ProjectionVersion
{
    /// <summary>
    /// Czy zdarzenie w wersji <paramref name="incoming"/> jest nowsze od już zastosowanej <paramref name="known"/>.
    /// Wersja 0 = zdarzenie sprzed wersjonowania: stosowane tylko, dopóki projekcja nie zna żadnej wersji.
    /// </summary>
    public static bool IsNewer(int incoming, int known) => incoming == 0 ? known == 0 : incoming > known;
}
