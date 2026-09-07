using ZipZap.Modules.Integrations.Ports;

namespace ZipZap.Modules.Integrations;

/// <summary>
/// Rejestr sterowników po kluczu (plugin/adapter pattern). Pozwala wybrać
/// właściwy sterownik per sklep w przyszłej implementacji integracji.
/// </summary>
public sealed class IntegrationDriverRegistry
{
    private readonly Dictionary<string, IFiscalPrinterDriver> _fiscalPrinters;
    private readonly Dictionary<string, IPosConnector> _posConnectors;

    public IntegrationDriverRegistry(
        IEnumerable<IFiscalPrinterDriver> fiscalPrinters,
        IEnumerable<IPosConnector> posConnectors)
    {
        _fiscalPrinters = fiscalPrinters.ToDictionary(d => d.Key, StringComparer.OrdinalIgnoreCase);
        _posConnectors = posConnectors.ToDictionary(c => c.Key, StringComparer.OrdinalIgnoreCase);
    }

    public IReadOnlyCollection<string> FiscalPrinterKeys => _fiscalPrinters.Keys.ToArray();
    public IReadOnlyCollection<string> PosConnectorKeys => _posConnectors.Keys.ToArray();

    public IFiscalPrinterDriver? GetFiscalPrinter(string key)
        => _fiscalPrinters.GetValueOrDefault(key);

    public IPosConnector? GetPosConnector(string key)
        => _posConnectors.GetValueOrDefault(key);
}
