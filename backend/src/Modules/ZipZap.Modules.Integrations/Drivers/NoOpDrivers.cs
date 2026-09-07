using ZipZap.Modules.Integrations.Ports;

namespace ZipZap.Modules.Integrations.Drivers;

/// <summary>No-op sterownik fiskalny do testów/dev — nic nie drukuje.</summary>
public sealed class NoOpFiscalPrinterDriver : IFiscalPrinterDriver
{
    public string Key => "noop";

    public Task<FiscalReceiptResult> PrintReceiptAsync(FiscalReceipt receipt, CancellationToken ct = default)
        => Task.FromResult(new FiscalReceiptResult(true, DocumentNumber: $"NOOP-{receipt.OrderId:N}", Error: null));

    public Task<DriverStatus> GetStatusAsync(CancellationToken ct = default)
        => Task.FromResult(new DriverStatus(FiscalDriverState.Online, "no-op driver"));
}

/// <summary>No-op konektor POS do testów/dev — nic nie synchronizuje.</summary>
public sealed class NoOpPosConnector : IPosConnector
{
    public string Key => "noop";

    public Task PushOrderAsync(PosOrder order, CancellationToken ct = default) => Task.CompletedTask;

    public Task<PosSyncResult> SyncCatalogAsync(Guid storeId, CancellationToken ct = default)
        => Task.FromResult(new PosSyncResult(true, SyncedItems: 0, Error: null));
}
