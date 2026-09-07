namespace ZipZap.Modules.Integrations.Ports;

public sealed record FiscalReceiptLine(string Name, decimal Quantity, decimal UnitPrice, decimal VatRate);

public sealed record FiscalReceipt(Guid OrderId, IReadOnlyList<FiscalReceiptLine> Lines, decimal Total, string Currency);

public sealed record FiscalReceiptResult(bool Success, string? DocumentNumber, string? Error);

public enum FiscalDriverState { Unknown, Online, Offline, Error }

public sealed record DriverStatus(FiscalDriverState State, string Message);

/// <summary>
/// Port sterownika kasy/drukarki fiskalnej (Novitus, Elzab, Posnet...).
/// Adapter/plugin — realne implementacje w przyszłości; teraz tylko interfejs.
/// </summary>
public interface IFiscalPrinterDriver
{
    string Key { get; }
    Task<FiscalReceiptResult> PrintReceiptAsync(FiscalReceipt receipt, CancellationToken ct = default);
    Task<DriverStatus> GetStatusAsync(CancellationToken ct = default);
}
