using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using ZipZap.Modules.Delivery.Infrastructure;
using ZipZap.Modules.Ordering.Domain;
using ZipZap.Modules.Ordering.Infrastructure;

namespace ZipZap.Api.IntegrationTests;

/// <summary>
/// S1b: lista zakupów i kompletacja w rundzie. Stały zegar: środa 3.04.2030 (UTC+2), runda 12:00 lokalnie
/// (10:00 UTC), cutoff 11:30 (09:30 UTC). Trzy zamówienia: K1 mleko×2, K2 mleko×1 + chleb×1, K3 chleb×3.
/// </summary>
[Collection("rounds")]
public sealed class RoundPickingTests
{
    private readonly RoundsApiFactory _f;
    public RoundPickingTests(RoundsApiFactory f) => _f = f;

    private static readonly DateOnly Wed = new(2030, 4, 3);
    private static DateTime Utc(int h, int m) => new(2030, 4, 3, h, m, 0, DateTimeKind.Utc);

    private sealed record Pick(Guid orderItemId, string status, int pickedQuantity, Guid? substituteProductId,
        string? substituteProductName, int? substituteQuantity, string? note, int version, string? updatedBy, DateTime? updatedAtUtc);
    private sealed record Line(Guid orderId, string orderCode, string customerCode, Guid orderItemId, Guid productId,
        string productName, string unit, int quantity, bool editable, Pick pick);
    private sealed record Row(Guid productId, string productName, string unit, int orderedQuantity, int boughtQuantity,
        int pendingLines, int boughtLines, int unavailableLines, int substitutedLines, List<Line> lines);
    private sealed record Progress(int lines, int pending, int bought, int unavailable, int substituted);
    private sealed record RoundInfo(string localDate, string localTime, DateTime startsAtUtc);
    private sealed record Summary(Guid? id, RoundInfo round, string state, string stateLabel, int orderCount,
        int excludedOrderCount, Progress progress);
    private sealed record RoundOrder(Guid orderId, string orderCode, string customerCode, string status, bool included,
        string? excludedReason, bool editable, List<Line> items);
    private sealed record Detail(Summary summary, List<Row> shoppingList, List<RoundOrder> orders);
    private sealed record Change(int version, string fromStatus, string status, int pickedQuantity,
        string? substituteProductName, int? substituteQuantity, string? note, string? changedBy, DateTime changedAtUtc);
    private sealed record OrderDto(Guid id, string status, decimal subtotal, List<ItemDto> items);
    private sealed record ItemDto(Guid productId, string productName, decimal unitPrice, int quantity);

    private sealed record World(OrderScenario.Setup S, Guid Bread, Guid Substitute, Guid[] Orders, AuthDto[] Customers,
        Guid RoundId, AuthDto Employee)
    {
        public string Admin => S.AdminToken;
    }

    private static string Code(AuthDto c) => "K-" + Guid.Parse(c.user.id).ToString("N")[..6].ToUpperInvariant();

    private async Task<World> RoundWithOrdersAsync(bool confirm = true)
    {
        _f.Clock.Set(Utc(8, 0)); // 10:00 lokalnie — przed terminem granicznym rundy 12:00
        var s = await OrderScenario.StoreWithSlotAsync(_f, slotDate: Wed);
        var bread = await OrderScenario.AddProductAsync(_f, s.StoreId, "Chleb");
        var substitute = await OrderScenario.AddProductAsync(_f, s.StoreId, "Chleb razowy", 6.50m);
        var customers = new[] { await _f.RegisterCustomerAsync(), await _f.RegisterCustomerAsync(), await _f.RegisterCustomerAsync() };
        var baskets = new[]
        {
            new[] { (s.ProductId, 2) },
            new[] { (s.ProductId, 1), (bread, 1) },
            new[] { (bread, 3) },
        };

        var orders = new Guid[3];
        for (var i = 0; i < 3; i++)
        {
            var cart = await OrderScenario.PrepareCartAsync(_f, s, customers[i].accessToken, baskets[i]);
            var resp = await OrderScenario.CheckoutCartAsync(_f, s, customers[i].accessToken, cart);
            resp.EnsureSuccessStatusCode();
            orders[i] = (await resp.Content.ReadFromJsonAsync<OrderDto>())!.id;
            if (confirm) // zamówienia płatne trafiają do zakupów po potwierdzeniu płatności
                (await _f.Authed(s.AdminToken).PostAsync($"/api/ordering/orders/{orders[i]}/confirm", null)).EnsureSuccessStatusCode();
        }

        var rounds = await ListAsync(_f.Authed(s.AdminToken), s.StoreId);
        var roundId = rounds.Single(r => r.id is not null).id!.Value;
        var employee = await _f.CreateEmployeeAsync(s.AdminToken, s.StoreId.ToString());
        return new World(s, bread, substitute, orders, customers, roundId, employee);
    }

    private static async Task<List<Summary>> ListAsync(HttpClient c, Guid storeId)
        => (await c.GetFromJsonAsync<List<Summary>>($"/api/ordering/stores/{storeId}/purchasing-rounds"))!;

    private Task<Detail> DetailAsync(World w, string? token = null)
        => _f.Authed(token ?? w.Admin).GetFromJsonAsync<Detail>(
            $"/api/ordering/stores/{w.S.StoreId}/purchasing-rounds/{w.RoundId}")!;

    private static Line LineOf(Detail d, Guid orderId, Guid productId)
        => d.orders.Single(o => o.orderId == orderId).items.Single(i => i.productId == productId);

    private Task<HttpResponseMessage> PickAsync(World w, string token, Guid orderItemId, object body)
        => _f.Authed(token).PutAsJsonAsync(
            $"/api/ordering/stores/{w.S.StoreId}/purchasing-rounds/{w.RoundId}/items/{orderItemId}/pick", body);

    private async Task<List<Change>> HistoryAsync(World w, Guid orderItemId)
        => (await _f.Authed(w.Admin).GetFromJsonAsync<List<Change>>(
            $"/api/ordering/stores/{w.S.StoreId}/purchasing-rounds/{w.RoundId}/items/{orderItemId}/pick-history"))!;

    [Fact]
    public async Task Shopping_list_sums_products_across_orders_and_keeps_every_order_and_customer()
    {
        var w = await RoundWithOrdersAsync();

        var raw = await _f.Authed(w.Admin).GetStringAsync($"/api/ordering/stores/{w.S.StoreId}/purchasing-rounds/{w.RoundId}");
        var d = await DetailAsync(w);

        d.summary.orderCount.Should().Be(3);
        d.summary.round.localTime.Should().Be("12:00:00");
        var milk = d.shoppingList.Single(r => r.productId == w.S.ProductId);
        milk.orderedQuantity.Should().Be(3);
        milk.lines.Select(l => (l.orderId, l.customerCode, l.quantity)).Should().BeEquivalentTo(new[]
            { (w.Orders[0], Code(w.Customers[0]), 2), (w.Orders[1], Code(w.Customers[1]), 1) });
        var bread = d.shoppingList.Single(r => r.productId == w.Bread);
        bread.orderedQuantity.Should().Be(4);
        bread.lines.Select(l => (l.orderId, l.quantity)).Should().BeEquivalentTo(new[] { (w.Orders[1], 1), (w.Orders[2], 3) });
        d.orders.Select(o => o.orderId).Should().BeEquivalentTo(w.Orders);

        // Minimum danych osobowych: bez adresu i telefonu klienta w widoku kompletacji.
        raw.Should().NotContain("Testowa").And.NotContain("600100200");
    }

    [Fact]
    public async Task Unpaid_and_cancelled_orders_are_listed_with_a_reason_but_not_shopped()
    {
        var w = await RoundWithOrdersAsync(confirm: false);

        var d = await DetailAsync(w);
        d.summary.orderCount.Should().Be(0);
        d.summary.excludedOrderCount.Should().Be(3);
        d.shoppingList.Should().BeEmpty();
        d.orders.Should().OnlyContain(o => !o.included && o.excludedReason == "Czeka na płatność");

        (await _f.Authed(w.Admin).PostAsync($"/api/ordering/orders/{w.Orders[0]}/confirm", null)).EnsureSuccessStatusCode();
        (await _f.Authed(w.Admin).PostAsync($"/api/ordering/orders/{w.Orders[1]}/cancel", null)).EnsureSuccessStatusCode();
        d = await DetailAsync(w);
        d.shoppingList.Should().ContainSingle().Which.productId.Should().Be(w.S.ProductId);
        d.orders.Single(o => o.orderId == w.Orders[1]).excludedReason.Should().Be("Anulowane");

        var cancelledItem = d.orders.Single(o => o.orderId == w.Orders[1]).items[0].orderItemId;
        (await PickAsync(w, w.Admin, cancelledItem, new { status = "Bought", pickedQuantity = 1, expectedVersion = 0 }))
            .StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Picking_records_bought_unavailable_and_substitution_with_author_and_time()
    {
        var w = await RoundWithOrdersAsync();
        var d = await DetailAsync(w);
        _f.Clock.Set(Utc(10, 5)); // w trakcie zakupów
        var emp = w.Employee.accessToken;

        var bought = (await (await PickAsync(w, emp, LineOf(d, w.Orders[0], w.S.ProductId).orderItemId,
            new { status = "Bought", pickedQuantity = 2, expectedVersion = 0 })).EnsureSuccessStatusCode()
            .Content.ReadFromJsonAsync<Pick>())!;
        bought.status.Should().Be("Bought");
        bought.version.Should().Be(1);
        bought.updatedBy.Should().Be(w.Employee.user.email);
        bought.updatedAtUtc.Should().Be(Utc(10, 5));

        (await PickAsync(w, emp, LineOf(d, w.Orders[1], w.S.ProductId).orderItemId,
            new { status = "unavailable", expectedVersion = 0 })).EnsureSuccessStatusCode();

        var breadItem = LineOf(d, w.Orders[2], w.Bread).orderItemId;
        var sub = (await (await PickAsync(w, emp, breadItem, new
        {
            status = "Substituted", substituteProductId = w.Substitute, substituteQuantity = 2,
            note = "klient zgodził się telefonicznie", expectedVersion = 0,
        })).EnsureSuccessStatusCode().Content.ReadFromJsonAsync<Pick>())!;
        (sub.substituteProductName, sub.substituteQuantity, sub.pickedQuantity).Should().Be(("Chleb razowy", (int?)2, 0));

        d = await DetailAsync(w);
        var milk = d.shoppingList.Single(r => r.productId == w.S.ProductId);
        (milk.boughtQuantity, milk.boughtLines, milk.unavailableLines).Should().Be((2, 1, 1));
        d.summary.progress.Should().Be(new Progress(4, 1, 1, 1, 1));

        var history = await HistoryAsync(w, breadItem);
        history.Should().ContainSingle();
        (history[0].fromStatus, history[0].status, history[0].changedBy, history[0].changedAtUtc, history[0].note)
            .Should().Be(("Pending", "Substituted", w.Employee.user.email, Utc(10, 5), "klient zgodził się telefonicznie"));

        // Oryginalna pozycja i kwoty zamówienia bez zmian; cena w katalogu bez zmian.
        var order = (await _f.Authed(w.Admin).GetFromJsonAsync<OrderDto>($"/api/ordering/orders/{w.Orders[2]}"))!;
        order.items.Should().ContainSingle().Which.Should().Be(new ItemDto(w.Bread, "Chleb", 5.00m, 3));
        order.subtotal.Should().Be(15.00m);
        using var scope = _f.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OrderingDbContext>();
        (await db.CatalogProducts.SingleAsync(p => p.Id == w.Substitute)).Price.Should().Be(6.50m);
    }

    [Fact]
    public async Task Corrections_are_versioned_and_stale_or_repeated_writes_never_overwrite_newer_state()
    {
        var w = await RoundWithOrdersAsync();
        var item = LineOf(await DetailAsync(w), w.Orders[1], w.S.ProductId).orderItemId;
        var emp = w.Employee.accessToken;

        (await PickAsync(w, emp, item, new { status = "Unavailable", expectedVersion = 0 })).EnsureSuccessStatusCode();
        var fixedPick = (await (await PickAsync(w, emp, item, new { status = "Bought", pickedQuantity = 1, expectedVersion = 1 }))
            .EnsureSuccessStatusCode().Content.ReadFromJsonAsync<Pick>())!;
        fixedPick.version.Should().Be(2);

        // Podwójne kliknięcie tej samej poprawki (z nieaktualną wersją) — bez błędu i bez nowego wpisu.
        var repeat = (await (await PickAsync(w, emp, item, new { status = "Bought", pickedQuantity = 1, expectedVersion = 1 }))
            .EnsureSuccessStatusCode().Content.ReadFromJsonAsync<Pick>())!;
        repeat.version.Should().Be(2);

        // Inna zmiana na podstawie starej wersji — odrzucona, nowszy stan zostaje.
        (await PickAsync(w, emp, item, new { status = "Unavailable", expectedVersion = 1 }))
            .StatusCode.Should().Be(HttpStatusCode.Conflict);
        LineOf(await DetailAsync(w), w.Orders[1], w.S.ProductId).pick.Should().Match<Pick>(p => p.status == "Bought" && p.version == 2);

        (await HistoryAsync(w, item)).Select(h => (h.version, h.fromStatus, h.status))
            .Should().Equal((1, "Pending", "Unavailable"), (2, "Unavailable", "Bought"));
    }

    [Fact]
    public async Task Parallel_updates_of_one_item_let_exactly_one_change_win()
    {
        var w = await RoundWithOrdersAsync();
        var item = LineOf(await DetailAsync(w), w.Orders[2], w.Bread).orderItemId;
        var bodies = new object[]
        {
            new { status = "Bought", pickedQuantity = 1, expectedVersion = 0 },
            new { status = "Bought", pickedQuantity = 2, expectedVersion = 0 },
            new { status = "Bought", pickedQuantity = 3, expectedVersion = 0 },
            new { status = "Unavailable", expectedVersion = 0 },
            new { status = "Unavailable", note = "brak na półce", expectedVersion = 0 },
            new { status = "Substituted", substituteProductId = w.Substitute, substituteQuantity = 1, expectedVersion = 0 },
        };

        var responses = await OrderScenario.FireTogetherAsync(bodies.Select(b => (Func<Task<HttpResponseMessage>>)(
            () => PickAsync(w, w.Employee.accessToken, item, b))).ToList());

        responses.Count(r => r.IsSuccessStatusCode).Should().Be(1);
        responses.Where(r => !r.IsSuccessStatusCode).Should().OnlyContain(r => r.StatusCode == HttpStatusCode.Conflict);
        (await HistoryAsync(w, item)).Should().ContainSingle().Which.version.Should().Be(1);

        // Równoległe powtórzenia TEJ SAMEJ zmiany — wszystkie OK, jeden wpis historii.
        var same = new { status = "Unavailable", note = "ostatecznie brak", expectedVersion = 1 };
        var repeats = await OrderScenario.FireTogetherAsync(Enumerable.Range(0, 4).Select(_ => (Func<Task<HttpResponseMessage>>)(
            () => PickAsync(w, w.Employee.accessToken, item, same))).ToList());
        repeats.Should().OnlyContain(r => r.IsSuccessStatusCode);
        (await HistoryAsync(w, item)).Select(h => h.version).Should().Equal(1, 2);
    }

    [Fact]
    public async Task Only_admin_and_staff_of_that_store_can_view_or_pick_drivers_and_others_cannot()
    {
        var w = await RoundWithOrdersAsync();
        var item = LineOf(await DetailAsync(w), w.Orders[0], w.S.ProductId).orderItemId;
        var otherStore = await _f.CreateStoreAsync(w.Admin);
        var outsiders = new[]
        {
            await _f.CreateEmployeeAsync(w.Admin, otherStore),                         // pracownik innego sklepu
            await _f.CreateStaffAsync(w.Admin, w.S.StoreId.ToString(), "Driver"),      // kierowca TEGO sklepu
            w.Customers[0],                                                            // klient tej rundy
        };
        var basePath = $"/api/ordering/stores/{w.S.StoreId}/purchasing-rounds";

        foreach (var who in outsiders)
        {
            var c = _f.Authed(who.accessToken);
            (await c.GetAsync(basePath)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
            (await c.GetAsync($"{basePath}/{w.RoundId}")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
            (await c.GetAsync($"{basePath}/{w.RoundId}/items/{item}/pick-history")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
            (await PickAsync(w, who.accessToken, item, new { status = "Bought", pickedQuantity = 2, expectedVersion = 0 }))
                .StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }
        (await _f.Anon().GetAsync(basePath)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        LineOf(await DetailAsync(w), w.Orders[0], w.S.ProductId).pick.version.Should().Be(0, "odmowy niczego nie zapisały");
        (await DetailAsync(w, w.Employee.accessToken)).orders.Should().HaveCount(3);
    }

    [Fact]
    public async Task Orders_without_a_round_do_not_break_round_views()
    {
        var w = await RoundWithOrdersAsync();
        Guid legacyId;
        using (var scope = _f.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<OrderingDbContext>();
            // Zamówienie sprzed S1a — bez rundy.
            var legacy = Order.Place(w.S.StoreId, Guid.Parse(w.Customers[0].user.id),
                new[] { new OrderLine(w.S.ProductId, "Mleko 1l", 4.00m, "szt", 1) }, 0.10m, 8.00m,
                w.S.ZoneId, w.S.SlotId, "ul. Stara 1", "600000000");
            legacy.Confirm();
            db.Orders.Add(legacy);
            await db.SaveChangesAsync();
            legacyId = legacy.Id;
        }

        var admin = _f.Authed(w.Admin);
        (await ListAsync(admin, w.S.StoreId)).Should().Contain(r => r.id == w.RoundId);
        (await DetailAsync(w)).orders.Select(o => o.orderId).Should().NotContain(legacyId);
        (await admin.GetStringAsync($"/api/ordering/stores/{w.S.StoreId}/orders")).Should().Contain(legacyId.ToString());
    }

    [Fact]
    public async Task Picking_never_changes_order_status_nor_emits_delivery_events()
    {
        var w = await RoundWithOrdersAsync();
        var d = await DetailAsync(w);
        _f.Clock.Set(Utc(9, 45)); // po terminie granicznym, przed rundą
        (await DetailAsync(w)).summary.state.Should().Be("to_shop");

        _f.Clock.Set(Utc(10, 10));
        (await DetailAsync(w)).summary.state.Should().Be("shopping");
        foreach (var line in d.orders.SelectMany(o => o.items))
            (await PickAsync(w, w.Employee.accessToken, line.orderItemId,
                new { status = "Bought", pickedQuantity = line.quantity, expectedVersion = 0 })).EnsureSuccessStatusCode();

        (await DetailAsync(w)).summary.state.Should().Be("picked");

        using var scope = _f.Services.CreateScope();
        var ordering = scope.ServiceProvider.GetRequiredService<OrderingDbContext>();
        var delivery = scope.ServiceProvider.GetRequiredService<DeliveryDbContext>();
        var orders = await ordering.Orders.AsNoTracking().Include(o => o.History)
            .Where(o => w.Orders.Contains(o.Id)).ToListAsync();
        orders.Should().OnlyContain(o => o.Status == OrderStatus.Confirmed && o.History.Count == 2); // Placed → Confirmed

        var keys = w.Orders.Select(id => id.ToString()).ToList();
        var progressEvents = new[] { "OrderReadyForPickup", "OrderPickedUp", "OrderDelivered" };
        foreach (var msgs in new[]
                 {
                     await ordering.OutboxMessages.AsNoTracking().Select(m => new { m.Type, m.Payload }).ToListAsync(),
                     await delivery.OutboxMessages.AsNoTracking().Select(m => new { m.Type, m.Payload }).ToListAsync(),
                 })
            msgs.Where(m => keys.Any(k => m.Payload.Contains(k)))
                .Should().NotContain(m => progressEvents.Any(e => m.Type.EndsWith("." + e)));
        (await delivery.Deliveries.AsNoTracking().CountAsync(x => w.Orders.Contains(x.OrderId))).Should().Be(0);
    }

    [Fact]
    public async Task Empty_states_are_explicit()
    {
        _f.Clock.Set(Utc(8, 0));
        var s = await OrderScenario.StoreWithSlotAsync(_f, slotDate: Wed);
        var admin = _f.Authed(s.AdminToken);

        // Sklep bez zamówień: najbliższa runda z harmonogramu, 0 zamówień, bez błędu.
        var rounds = await ListAsync(admin, s.StoreId);
        rounds.Should().ContainSingle();
        (rounds[0].id, rounds[0].state, rounds[0].orderCount, rounds[0].round.localTime)
            .Should().Be(((Guid?)null, "accepting", 0, "12:00:00"));

        (await admin.GetAsync($"/api/ordering/stores/{s.StoreId}/purchasing-rounds/{Guid.NewGuid()}"))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);

        // Runda, której jedyne zamówienie anulowano — po terminie granicznym stan „brak zamówień".
        var c = await _f.RegisterCustomerAsync();
        var order = (await (await OrderScenario.CheckoutAsync(_f, s, c.accessToken)).EnsureSuccessStatusCode()
            .Content.ReadFromJsonAsync<OrderDto>())!;
        (await admin.PostAsync($"/api/ordering/orders/{order.id}/cancel", null)).EnsureSuccessStatusCode();
        _f.Clock.Set(Utc(9, 45));
        var round = (await ListAsync(admin, s.StoreId)).Single(r => r.id is not null);
        (round.state, round.orderCount, round.excludedOrderCount).Should().Be(("empty", 0, 1));
    }
}
