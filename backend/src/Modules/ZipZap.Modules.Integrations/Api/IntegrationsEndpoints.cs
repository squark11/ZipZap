using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace ZipZap.Modules.Integrations.Api;

public static class IntegrationsEndpoints
{
    public static IEndpointRouteBuilder MapIntegrationsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/integrations").WithTags("Integrations");

        // Podgląd zarejestrowanych sterowników (szkielet — na start tylko no-op).
        group.MapGet("/drivers", (IntegrationDriverRegistry registry) =>
            Results.Ok(new
            {
                FiscalPrinters = registry.FiscalPrinterKeys,
                PosConnectors = registry.PosConnectorKeys,
            }))
            .RequireAuthorization("Admin");

        return app;
    }
}
