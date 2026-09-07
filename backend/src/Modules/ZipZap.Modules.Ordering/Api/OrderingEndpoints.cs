using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using ZipZap.BuildingBlocks.Domain;
using ZipZap.Modules.Ordering.Application;

namespace ZipZap.Modules.Ordering.Api;

public static class OrderingEndpoints
{
    public static IEndpointRouteBuilder MapOrderingEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/ordering").WithTags("Ordering");

        // ---------- Koszyk (publiczny, chroniony tokenem koszyka) ----------

        group.MapPost("/carts", async (CreateCartRequest req, OrderingService svc, CancellationToken ct) =>
            Respond(await svc.CreateCartAsync(req.StoreId, ct)));

        group.MapGet("/carts/{cartId:guid}", async (Guid cartId, string token, OrderingService svc, CancellationToken ct) =>
            Respond(await svc.GetCartAsync(cartId, token, ct)));

        group.MapPost("/carts/{cartId:guid}/items", async (Guid cartId, string token, AddCartItemRequest req, OrderingService svc, CancellationToken ct) =>
            Respond(await svc.AddItemAsync(cartId, token, req.ProductId, req.Quantity, ct)));

        group.MapPut("/carts/{cartId:guid}/items/{productId:guid}", async (Guid cartId, Guid productId, string token, SetCartItemQuantityRequest req, OrderingService svc, CancellationToken ct) =>
            Respond(await svc.SetItemQuantityAsync(cartId, token, productId, req.Quantity, ct)));

        group.MapDelete("/carts/{cartId:guid}/items/{productId:guid}", async (Guid cartId, Guid productId, string token, OrderingService svc, CancellationToken ct) =>
            Respond(await svc.RemoveItemAsync(cartId, token, productId, ct)));

        // ---------- Strefy dostaw i sloty ----------

        group.MapGet("/stores/{storeId:guid}/zones", async (Guid storeId, OrderingService svc, CancellationToken ct) =>
            Results.Ok(await svc.ListZonesAsync(storeId, ct)));

        group.MapGet("/stores/{storeId:guid}/slots", async (Guid storeId, Guid? zoneId, DateOnly? date, OrderingService svc, CancellationToken ct) =>
            Results.Ok(await svc.ListAvailableSlotsAsync(storeId, zoneId, date, ct)));

        group.MapPost("/stores/{storeId:guid}/zones", async (Guid storeId, CreateZoneRequest req, OrderingService svc, CancellationToken ct) =>
            Respond(await svc.CreateZoneAsync(storeId, req.Name, req.DeliveryFee, req.PostalCodes, ct)))
            .RequireAuthorization("StoreEmployee");

        group.MapPost("/stores/{storeId:guid}/slots", async (Guid storeId, CreateSlotRequest req, OrderingService svc, CancellationToken ct) =>
            Respond(await svc.CreateSlotAsync(storeId, req.DeliveryZoneId, req.Date, req.StartTime, req.EndTime, req.MaxOrders, ct)))
            .RequireAuthorization("StoreEmployee");

        // ---------- Zamówienia ----------

        group.MapPost("/carts/{cartId:guid}/checkout", async (Guid cartId, PlaceOrderRequest req, OrderingService svc, CancellationToken ct) =>
            Respond(await svc.PlaceOrderAsync(cartId, req.Token, req.DeliveryZoneId, req.TimeSlotId, req.DeliveryAddress, req.ContactPhone, ct)))
            .RequireAuthorization(); // wymaga zalogowanego klienta (rejestracja przy płatności)

        group.MapGet("/orders/mine", async (OrderingService svc, CancellationToken ct) =>
            Results.Ok(await svc.ListMyOrdersAsync(ct)))
            .RequireAuthorization();

        group.MapGet("/orders/{orderId:guid}", async (Guid orderId, OrderingService svc, CancellationToken ct) =>
            Respond(await svc.GetOrderAsync(orderId, ct)))
            .RequireAuthorization();

        group.MapGet("/stores/{storeId:guid}/orders", async (Guid storeId, string? status, OrderingService svc, CancellationToken ct) =>
            Results.Ok(await svc.ListStoreOrdersAsync(storeId, status, ct)))
            .RequireAuthorization("StoreEmployee");

        // Przejścia statusów (autoryzacja per-akcja w serwisie)
        MapTransition(group, "confirm", OrderAction.Confirm);
        MapTransition(group, "start-picking", OrderAction.StartPicking);
        MapTransition(group, "ready", OrderAction.Ready);
        MapTransition(group, "pick-up", OrderAction.PickUp);
        MapTransition(group, "delivered", OrderAction.Delivered);
        MapTransition(group, "complete", OrderAction.Complete);
        MapTransition(group, "cancel", OrderAction.Cancel);

        return app;
    }

    private static void MapTransition(RouteGroupBuilder group, string path, OrderAction action)
    {
        group.MapPost($"/orders/{{orderId:guid}}/{path}",
            async (Guid orderId, OrderingService svc, CancellationToken ct) =>
                Respond(await svc.ChangeStatusAsync(orderId, action, ct)))
            .RequireAuthorization();
    }

    private static IResult Respond<T>(Result<T> result)
        => result.IsSuccess
            ? Results.Ok(result.Value)
            : Results.Problem(detail: result.Error.Message, statusCode: result.Error.ToStatusCode(), title: result.Error.Code);
}
