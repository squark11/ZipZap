using Microsoft.Extensions.DependencyInjection;
using ZipZap.Modules.Integrations.Drivers;
using ZipZap.Modules.Integrations.Ports;

namespace ZipZap.Modules.Integrations;

public static class IntegrationsModule
{
    /// <summary>
    /// Rejestruje szkielet integracji: porty + no-op sterowniki + rejestr.
    /// Realne adaptery (Novitus/Elzab/Posnet, Comarch Optima) dodasz później
    /// jako kolejne implementacje IFiscalPrinterDriver / IPosConnector.
    /// </summary>
    public static IServiceCollection AddIntegrationsModule(this IServiceCollection services)
    {
        services.AddSingleton<IFiscalPrinterDriver, NoOpFiscalPrinterDriver>();
        services.AddSingleton<IPosConnector, NoOpPosConnector>();
        services.AddSingleton<IntegrationDriverRegistry>();
        return services;
    }
}
