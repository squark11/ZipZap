namespace ZipZap.Modules.Integrations.Ports;

public sealed record PosOrderLine(string Sku, string Name, decimal Quantity, decimal UnitPrice);

public sealed record PosOrder(Guid OrderId, Guid StoreId, IReadOnlyList<PosOrderLine> Lines, decimal Total);

public sealed record PosSyncResult(bool Success, int SyncedItems, string? Error);

/// <summary>
/// Port konektora POS/ERP sklepu (np. Comarch Optima).
/// Adapter/plugin — realne implementacje w przyszłości; teraz tylko interfejs.
/// </summary>
public interface IPosConnector
{
    string Key { get; }
    Task PushOrderAsync(PosOrder order, CancellationToken ct = default);
    Task<PosSyncResult> SyncCatalogAsync(Guid storeId, CancellationToken ct = default);
}
