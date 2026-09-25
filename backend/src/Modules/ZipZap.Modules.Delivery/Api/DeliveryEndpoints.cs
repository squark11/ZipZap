using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using ZipZap.BuildingBlocks.Auditing;
using ZipZap.BuildingBlocks.Domain;
using ZipZap.BuildingBlocks.MultiTenancy;
using ZipZap.Modules.Delivery.Application;
using ZipZap.Modules.Delivery.Infrastructure;

namespace ZipZap.Modules.Delivery.Api;

public static class DeliveryEndpoints
{
    public static IEndpointRouteBuilder MapDeliveryEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/delivery").WithTags("Delivery");

        // ---------- Kierowca ----------

        // Pula nieprzypisanych dostaw (bez danych klienta) — podgląd; przydziela operator sklepu.
        group.MapGet("/available", async (DeliveryService svc, CancellationToken ct) =>
            Results.Ok(await svc.ListAvailableAsync(ct)))
            .RequireAuthorization("Driver");

        // Moje dostawy: adres/telefon tylko dla przypisanych mi dostaw w realizacji (sprawdzenie w bazie).
        group.MapGet("/mine", async (DeliveryService svc, CancellationToken ct) =>
            Results.Ok(await svc.ListMineAsync(ct)))
            .RequireAuthorization("Driver");

        group.MapPost("/{deliveryId:guid}/accept", async (Guid deliveryId, DeliveryService svc, CancellationToken ct) =>
            Respond(await svc.AcceptAsync(deliveryId, ct)))
            .RequireAuthorization("Driver")
            .WithSummary("Wyłączone w S1c — dostawy przydziela operator sklepu (403).");

        group.MapPost("/{deliveryId:guid}/pick-up", async (Guid deliveryId, DeliveryService svc, IAuditLogger audit, CancellationToken ct) =>
            await Audited(await svc.PickUpAsync(deliveryId, ct), "delivery.picked_up", audit, ct))
            .RequireAuthorization("Driver")
            .WithSummary("Przypisany kierowca odbiera zamówienie (→ OrderPickedUp).");

        group.MapPost("/{deliveryId:guid}/delivered", async (Guid deliveryId, DeliveryService svc, IAuditLogger audit, CancellationToken ct) =>
            await Audited(await svc.DeliverAsync(deliveryId, ct), "delivery.delivered", audit, ct))
            .RequireAuthorization("Driver")
            .WithSummary("Przypisany kierowca oznacza dostarczenie (→ OrderDelivered, dokładnie raz).");

        // ---------- Administrator: awaryjna zmiana (osobna akcja, NIE wyjątek w endpointach kierowcy) ----------

        group.MapPost("/admin/deliveries/{deliveryId:guid}/override", async (Guid deliveryId, AdminOverrideInput req,
            DeliveryService svc, IAuditLogger audit, CancellationToken ct) =>
        {
            var result = await svc.OverrideAsync(deliveryId, req, ct);
            if (result.IsSuccess)
            {
                var d = result.Value;
                await audit.LogAsync("delivery.admin_override", "delivery", d.Id.ToString(), d.StoreId,
                    new { d.OrderId, action = req.Action, reason = req.Reason?.Trim(), d.DriverId, d.Status, d.Version }, ct);
            }
            return Respond(result);
        })
            .RequireAuthorization("Admin")
            .WithSummary("Awaryjne potwierdzenie odbioru/dostarczenia przez administratora — wymagany powód, audyt.");

        group.MapGet("/orders/{orderId:guid}", async (Guid orderId, DeliveryDbContext db, ICurrentUser user, CancellationToken ct) =>
        {
            var d = await db.Deliveries.AsNoTracking().FirstOrDefaultAsync(x => x.OrderId == orderId, ct);
            if (d is null) return Results.NotFound();
            // Izolacja: dostawę widzi obsługa sklepu (dowolna z lokalizacji), przypisany kierowca lub admin. Bez danych klienta.
            var allowed = user.ManagesStore(d.StoreId) || (d.DriverId is Guid drv && user.UserId == drv);
            return allowed ? Results.Ok(DeliveryDto.From(d)) : Results.Forbid();
        }).RequireAuthorization();

        // ---------- Operator sklepu (pracownik sklepu / admin) ----------

        group.MapGet("/stores/{storeId:guid}/deliveries", async (Guid storeId, DeliveryService svc, CancellationToken ct) =>
            Respond(await svc.ListForStoreAsync(storeId, ct)))
            .RequireAuthorization("StoreEmployee");

        group.MapGet("/stores/{storeId:guid}/board", async (Guid storeId, DateOnly? date, string? status,
            DeliveryService svc, CancellationToken ct) =>
            Respond(await svc.GetBoardAsync(storeId, date, status, ct)))
            .RequireAuthorization("StoreEmployee")
            .WithSummary("Dostawy sklepu (filtr dnia i statusu) z adresem/telefonem + aktywni kierowcy sklepu.");

        group.MapPost("/stores/{storeId:guid}/deliveries/{deliveryId:guid}/assign", async (Guid storeId, Guid deliveryId,
            AssignDriverInput req, DeliveryService svc, IAuditLogger audit, CancellationToken ct) =>
            await Audited(await svc.AssignAsync(storeId, deliveryId, req, ct), "delivery.assigned", audit, ct))
            .RequireAuthorization("StoreEmployee")
            .WithSummary("Przypisanie dostawy aktywnemu kierowcy sklepu (z oczekiwaną wersją).");

        group.MapPost("/stores/{storeId:guid}/deliveries/{deliveryId:guid}/unassign", async (Guid storeId, Guid deliveryId,
            UnassignDriverInput req, DeliveryService svc, IAuditLogger audit, CancellationToken ct) =>
            await Audited(await svc.UnassignAsync(storeId, deliveryId, req, ct), "delivery.unassigned", audit, ct))
            .RequireAuthorization("StoreEmployee");

        group.MapPut("/stores/{storeId:guid}/route", async (Guid storeId, RouteOrderInput req, DeliveryService svc,
            IAuditLogger audit, CancellationToken ct) =>
        {
            var result = await svc.ReorderRouteAsync(storeId, req, ct);
            if (result.IsSuccess)
                await audit.LogAsync("delivery.route_reordered", "delivery_route", req.DriverId.ToString(), storeId,
                    new { req.DeliveryDate, req.WindowStart, req.WindowEnd, stops = result.Value.Select(d => d.Id) }, ct);
            return Respond(result);
        })
            .RequireAuthorization("StoreEmployee")
            .WithSummary("Ręczna kolejność przystanków trasy kierowcy w oknie dostawy.");

        group.MapGet("/stores/{storeId:guid}/deliveries/{deliveryId:guid}/history", async (Guid storeId, Guid deliveryId,
            DeliveryService svc, CancellationToken ct) =>
            Respond(await svc.HistoryAsync(storeId, deliveryId, ct)))
            .RequireAuthorization("StoreEmployee");

        return app;
    }

    /// <summary>Wpis do audytu platformy (bez adresu/telefonu) po udanej zmianie.</summary>
    private static async Task<IResult> Audited(Result<DeliveryDto> result, string action, IAuditLogger audit, CancellationToken ct)
    {
        if (result.IsSuccess)
        {
            var d = result.Value;
            await audit.LogAsync(action, "delivery", d.Id.ToString(), d.StoreId,
                new { d.OrderId, d.DriverId, d.Status, d.StopSequence, d.Version }, ct);
        }
        return Respond(result);
    }

    private static IResult Respond<T>(Result<T> result)
        => result.IsSuccess
            ? Results.Ok(result.Value)
            : Results.Problem(detail: result.Error.Message, statusCode: result.Error.ToStatusCode(), title: result.Error.Code);
}
