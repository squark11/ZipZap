using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using ZipZap.Modules.Payments.Infrastructure;

namespace ZipZap.Modules.Payments.Api;

public static class PaymentsEndpoints
{
    public static IEndpointRouteBuilder MapPaymentsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/payments").WithTags("Payments");

        group.MapGet("/orders/{orderId:guid}", async (Guid orderId, PaymentsDbContext db, CancellationToken ct) =>
        {
            var p = await db.Payments.AsNoTracking().FirstOrDefaultAsync(x => x.OrderId == orderId, ct);
            return p is null
                ? Results.NotFound()
                : Results.Ok(new { p.OrderId, p.StoreId, p.Amount, p.CommissionAmount, Status = p.Status.ToString(), p.ProviderRef });
        }).RequireAuthorization();

        group.MapGet("/stores/{storeId:guid}/commission", async (Guid storeId, PaymentsDbContext db, CancellationToken ct) =>
        {
            var total = await db.CommissionLedger.Where(l => l.StoreId == storeId).SumAsync(l => (decimal?)l.Amount, ct) ?? 0m;
            var count = await db.CommissionLedger.CountAsync(l => l.StoreId == storeId, ct);
            return Results.Ok(new { StoreId = storeId, TotalCommission = total, Entries = count });
        }).RequireAuthorization("StoreEmployee");

        return app;
    }
}
