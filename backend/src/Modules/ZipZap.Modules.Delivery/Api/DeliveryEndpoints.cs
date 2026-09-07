using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using ZipZap.Modules.Delivery.Domain;
using ZipZap.Modules.Delivery.Infrastructure;

namespace ZipZap.Modules.Delivery.Api;

public static class DeliveryEndpoints
{
    public static IEndpointRouteBuilder MapDeliveryEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/delivery").WithTags("Delivery");

        // Dostawy dostępne do odbioru (dla kierowcy / admina).
        group.MapGet("/available", async (DeliveryDbContext db, CancellationToken ct) =>
        {
            var items = await db.Deliveries.AsNoTracking()
                .Where(d => d.Status == DeliveryStatus.AvailableForPickup)
                .OrderBy(d => d.CreatedAtUtc)
                .Select(d => new { d.Id, d.OrderId, d.StoreId, Status = d.Status.ToString(), d.CreatedAtUtc })
                .ToListAsync(ct);
            return Results.Ok(items);
        }).RequireAuthorization("Driver");

        group.MapGet("/orders/{orderId:guid}", async (Guid orderId, DeliveryDbContext db, CancellationToken ct) =>
        {
            var d = await db.Deliveries.AsNoTracking().FirstOrDefaultAsync(x => x.OrderId == orderId, ct);
            return d is null
                ? Results.NotFound()
                : Results.Ok(new { d.Id, d.OrderId, d.StoreId, d.DriverId, Status = d.Status.ToString(), d.PickedUpAtUtc, d.DeliveredAtUtc });
        }).RequireAuthorization();

        return app;
    }
}
