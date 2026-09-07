using ZipZap.BuildingBlocks.MultiTenancy;

namespace ZipZap.Api.Middleware;

/// <summary>
/// Ustala bieżącego najemcę (sklep) per-request: z claimu `store_id` (zalogowany
/// pracownik/kierowca) lub z nagłówka `X-Store-Id` (klient przeglądający bez logowania).
/// </summary>
public sealed class TenantResolutionMiddleware
{
    private readonly RequestDelegate _next;

    public TenantResolutionMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context, CurrentTenant tenant)
    {
        Guid? storeId = null;

        var claim = context.User?.FindFirst("store_id")?.Value;
        if (Guid.TryParse(claim, out var fromClaim))
            storeId = fromClaim;
        else if (context.Request.Headers.TryGetValue("X-Store-Id", out var header)
                 && Guid.TryParse(header, out var fromHeader))
            storeId = fromHeader;

        tenant.Set(storeId);
        await _next(context);
    }
}
