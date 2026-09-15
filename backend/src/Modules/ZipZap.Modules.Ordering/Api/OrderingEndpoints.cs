using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using ZipZap.BuildingBlocks.Auditing;
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

        group.MapPost("/carts/{cartId:guid}/checkout", async (Guid cartId, PlaceOrderRequest req, HttpContext http,
            OrderingService svc, IStoreLegalPolicyProvider legal, IAuditLogger audit, CancellationToken ct) =>
        {
            var idempotencyKey = http.Request.Headers.TryGetValue("Idempotency-Key", out var k) ? k.ToString() : null;
            var result = await svc.PlaceOrderAsync(cartId, req.Token, req.DeliveryZoneId, req.TimeSlotId,
                req.DeliveryAddress, req.ContactPhone, req.ConsentAccepted, idempotencyKey, ct);

            // Trwały dowód zgody: gdy sklep wymagał akceptacji, zapisz przyjęte dokumenty (URL + czas) w audycie.
            if (result.IsSuccess && req.ConsentAccepted)
            {
                var policy = await legal.GetAsync(result.Value.StoreId, ct);
                if (policy is { RequiresAcceptance: true })
                    await audit.LogAsync("order.consent.accepted", "order", result.Value.Id.ToString(), result.Value.StoreId,
                        new { result.Value.CustomerId, policy.TermsUrl, policy.PrivacyUrl, policy.GdprUrl, acceptedAtUtc = DateTime.UtcNow }, ct);
            }
            return Respond(result);
        })
        .RequireAuthorization(); // wymaga zalogowanego klienta (rejestracja przy płatności)

        group.MapGet("/orders/mine", async (OrderingService svc, CancellationToken ct) =>
            Results.Ok(await svc.ListMyOrdersAsync(ct)))
            .RequireAuthorization();

        group.MapGet("/orders/{orderId:guid}", async (Guid orderId, OrderingService svc, CancellationToken ct) =>
            Respond(await svc.GetOrderAsync(orderId, ct)))
            .RequireAuthorization();

        group.MapGet("/stores/{storeId:guid}/orders", async (Guid storeId, string? status, OrderingService svc, CancellationToken ct) =>
            Respond(await svc.ListStoreOrdersAsync(storeId, status, ct)))
            .RequireAuthorization("StoreEmployee");

        // Eksport zamówień do księgowości (CSV, per sklep + zakres dat + opcjonalny status).
        group.MapGet("/stores/{storeId:guid}/orders/export",
            async (Guid storeId, string? status, DateOnly? from, DateOnly? to, OrderingService svc, CancellationToken ct) =>
        {
            var result = await svc.ExportStoreOrdersAsync(storeId, status, from, to, ct);
            if (result.IsFailure)
                return Results.Problem(detail: result.Error.Message, statusCode: result.Error.ToStatusCode(), title: result.Error.Code);
            var bytes = System.Text.Encoding.UTF8.GetBytes("﻿" + result.Value); // BOM UTF‑8
            var fileName = $"zamowienia-{storeId:N}-{DateTime.UtcNow:yyyyMMdd}.csv";
            return Results.File(bytes, "text/csv; charset=utf-8", fileName);
        }).RequireAuthorization("StoreEmployee");

        // ---------- Adresy dostaw klienta (uwierzytelnione) ----------

        group.MapGet("/addresses", async (OrderingService svc, CancellationToken ct) =>
            Results.Ok(await svc.ListMyAddressesAsync(ct))).RequireAuthorization();

        group.MapPost("/addresses", async (AddressInput input, OrderingService svc, CancellationToken ct) =>
            Respond(await svc.AddAddressAsync(input, ct))).RequireAuthorization();

        group.MapPut("/addresses/{id:guid}", async (Guid id, AddressInput input, OrderingService svc, CancellationToken ct) =>
            Respond(await svc.UpdateAddressAsync(id, input, ct))).RequireAuthorization();

        group.MapDelete("/addresses/{id:guid}", async (Guid id, OrderingService svc, CancellationToken ct) =>
        {
            var r = await svc.DeleteAddressAsync(id, ct);
            return r.IsSuccess ? Results.NoContent()
                : Results.Problem(detail: r.Error.Message, statusCode: r.Error.ToStatusCode(), title: r.Error.Code);
        }).RequireAuthorization();

        group.MapPost("/addresses/{id:guid}/default", async (Guid id, OrderingService svc, CancellationToken ct) =>
        {
            var r = await svc.SetDefaultAddressAsync(id, ct);
            return r.IsSuccess ? Results.Ok()
                : Results.Problem(detail: r.Error.Message, statusCode: r.Error.ToStatusCode(), title: r.Error.Code);
        }).RequireAuthorization();

        // Przejścia statusów (autoryzacja per-akcja w serwisie)
        // Akcje sklepu/administracji. Odbiór i dostarczenie prowadzi kierowca
        // przez moduł Delivery (POST /api/delivery/{id}/pick-up | /delivered).
        MapTransition(group, "confirm", OrderAction.Confirm);
        MapTransition(group, "start-picking", OrderAction.StartPicking);
        MapTransition(group, "ready", OrderAction.Ready);
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
