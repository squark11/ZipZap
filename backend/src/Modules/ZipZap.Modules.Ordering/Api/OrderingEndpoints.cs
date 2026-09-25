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
            Respond(await svc.AddItemAsync(cartId, token, req.ProductId, req.Unit, req.Quantity, ct)));

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

        // ---------- Rundy zakupowe (osobne od terminów dostaw) ----------

        // Publiczne (bez logowania): najbliższa runda + termin graniczny + najwcześniejsza dostawa — PRZED zamówieniem.
        group.MapGet("/stores/{storeId:guid}/purchasing-round", async (Guid storeId, PurchasingRoundService rounds, CancellationToken ct) =>
            Respond(await rounds.PreviewAsync(storeId, ct)))
            .WithSummary("Najbliższa runda zakupowa sklepu (czas lokalny + UTC) i najwcześniejszy początek dostawy.");

        group.MapGet("/admin/purchasing-schedule", async (PurchasingRoundService rounds, CancellationToken ct) =>
            Results.Ok(await rounds.GetScheduleAsync(ct)))
            .RequireAuthorization("Admin");

        group.MapPut("/admin/purchasing-schedule", async (PurchasingScheduleDto req, PurchasingRoundService rounds,
            IAuditLogger audit, CancellationToken ct) =>
        {
            var result = await rounds.UpdateScheduleAsync(req, ct);
            if (result.IsSuccess)
                await audit.LogAsync("purchasing_schedule.updated", "purchasing_schedule", null, null, result.Value, ct);
            return Respond(result);
        })
            .RequireAuthorization("Admin")
            .WithSummary("Zmiana harmonogramu rund (godziny lokalne, cutoff, dni, czas do dostawy).");

        // ---------- Rundy: lista zakupów i kompletacja (admin + pracownik sklepu; kierowca — brak dostępu) ----------

        group.MapGet("/stores/{storeId:guid}/purchasing-rounds", async (Guid storeId, int? pastDays,
            RoundPickingService picking, CancellationToken ct) =>
            Respond(await picking.ListRoundsAsync(storeId, pastDays ?? 3, ct)))
            .RequireAuthorization("StoreEmployee")
            .WithSummary("Bieżące i nadchodzące rundy sklepu (+ ostatnie dni) ze stanem i postępem kompletacji.");

        group.MapGet("/stores/{storeId:guid}/purchasing-rounds/{roundId:guid}", async (Guid storeId, Guid roundId,
            RoundPickingService picking, CancellationToken ct) =>
            Respond(await picking.GetRoundAsync(storeId, roundId, ct)))
            .RequireAuthorization("StoreEmployee")
            .WithSummary("Runda: lista zakupów (suma + rozbicie na zamówienia) i zamówienia z kompletacją.");

        group.MapPut("/stores/{storeId:guid}/purchasing-rounds/{roundId:guid}/items/{orderItemId:guid}/pick",
            async (Guid storeId, Guid roundId, Guid orderItemId, PickUpdateInput req, RoundPickingService picking,
                IAuditLogger audit, CancellationToken ct) =>
        {
            var result = await picking.UpdatePickAsync(storeId, roundId, orderItemId, req, ct);
            // Pełny ślad (z notatką) jest w historii kompletacji; do audytu idzie skrót bez treści notatki.
            if (result.IsSuccess)
                await audit.LogAsync("round.item.picked", "order_item", orderItemId.ToString(), storeId,
                    new { roundId, result.Value.Status, result.Value.PickedQuantity, result.Value.SubstituteProductName,
                          result.Value.SubstituteQuantity, result.Value.Version }, ct);
            return Respond(result);
        })
            .RequireAuthorization("StoreEmployee")
            .WithSummary("Kompletacja pozycji: oczekuje / kupiono / niedostępne / zastąpiono (z wersją — bez nadpisywania nowszej zmiany).");

        group.MapGet("/stores/{storeId:guid}/purchasing-rounds/{roundId:guid}/items/{orderItemId:guid}/pick-history",
            async (Guid storeId, Guid roundId, Guid orderItemId, RoundPickingService picking, CancellationToken ct) =>
            Respond(await picking.GetPickHistoryAsync(storeId, roundId, orderItemId, ct)))
            .RequireAuthorization("StoreEmployee");

        // ---------- Zamówienia ----------

        group.MapPost("/carts/{cartId:guid}/checkout", async (Guid cartId, PlaceOrderRequest req, HttpContext http,
            OrderingService svc, IStoreLegalPolicyProvider legal, IAuditLogger audit, CancellationToken ct) =>
        {
            var idempotencyKey = http.Request.Headers.TryGetValue("Idempotency-Key", out var k) ? k.ToString() : null;
            var result = await svc.PlaceOrderAsync(cartId, req.Token, req.DeliveryZoneId, req.TimeSlotId,
                req.DeliveryAddress, req.ContactPhone, req.ConsentAccepted, idempotencyKey, ct,
                req.ExpectedRoundStartsAtUtc);

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
