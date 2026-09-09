using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using ZipZap.BuildingBlocks.Domain;
using ZipZap.Modules.Delivery.Application;
using ZipZap.Modules.Delivery.Infrastructure;

namespace ZipZap.Modules.Delivery.Api;

public static class DeliveryEndpoints
{
    public static IEndpointRouteBuilder MapDeliveryEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/delivery").WithTags("Delivery");

        // Pula dostępnych dostaw (dla kierowców).
        group.MapGet("/available", async (DeliveryService svc, CancellationToken ct) =>
            Results.Ok(await svc.ListAvailableAsync(ct)))
            .RequireAuthorization("Driver");

        // Moje dostawy (izolacja: tylko własne).
        group.MapGet("/mine", async (DeliveryService svc, CancellationToken ct) =>
            Results.Ok(await svc.ListMineAsync(ct)))
            .RequireAuthorization("Driver");

        group.MapPost("/{deliveryId:guid}/accept", async (Guid deliveryId, DeliveryService svc, CancellationToken ct) =>
            Respond(await svc.AcceptAsync(deliveryId, ct)))
            .RequireAuthorization("Driver")
            .WithSummary("Kierowca przyjmuje dostawę z puli.");

        group.MapPost("/{deliveryId:guid}/pick-up", async (Guid deliveryId, DeliveryService svc, CancellationToken ct) =>
            Respond(await svc.PickUpAsync(deliveryId, ct)))
            .RequireAuthorization("Driver")
            .WithSummary("Kierowca odbiera zamówienie (→ OrderPickedUp).");

        group.MapPost("/{deliveryId:guid}/delivered", async (Guid deliveryId, DeliveryService svc, CancellationToken ct) =>
            Respond(await svc.DeliverAsync(deliveryId, ct)))
            .RequireAuthorization("Driver")
            .WithSummary("Kierowca oznacza dostarczenie (→ OrderDelivered).");

        group.MapGet("/orders/{orderId:guid}", async (Guid orderId, DeliveryDbContext db, CancellationToken ct) =>
        {
            var d = await db.Deliveries.AsNoTracking().FirstOrDefaultAsync(x => x.OrderId == orderId, ct);
            return d is null ? Results.NotFound() : Results.Ok(DeliveryDto.From(d));
        }).RequireAuthorization();

        // Widok operacyjny sklepu: wszystkie dostawy sklepu (StoreEmployee/Admin).
        group.MapGet("/stores/{storeId:guid}/deliveries", async (Guid storeId, DeliveryService svc, CancellationToken ct) =>
        {
            var result = await svc.ListForStoreAsync(storeId, ct);
            return result.IsSuccess
                ? Results.Ok(result.Value)
                : Results.Problem(detail: result.Error.Message, statusCode: result.Error.ToStatusCode(), title: result.Error.Code);
        }).RequireAuthorization("StoreEmployee");

        return app;
    }

    private static IResult Respond(Result<DeliveryDto> result)
        => result.IsSuccess
            ? Results.Ok(result.Value)
            : Results.Problem(detail: result.Error.Message, statusCode: result.Error.ToStatusCode(), title: result.Error.Code);
}
