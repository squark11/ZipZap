using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using ZipZap.Modules.Delivery.Infrastructure;

namespace ZipZap.Api.IntegrationTests;

/// <summary>
/// S1c: przydział dostaw przez operatora, kolejność trasy, widok kierowcy. Dane klienta (adres/telefon):
/// operator — tylko swoich sklepów; kierowca — dopiero po przypisaniu KONKRETNEJ dostawy, przy aktywnym koncie
/// i aktualnym przypisaniu do sklepu (sprawdzane w bazie, nie w tokenie).
/// </summary>
[Collection("api")]
public sealed class DeliveryDispatchTests
{
    private readonly ApiFactory _f;
    public DeliveryDispatchTests(ApiFactory f) => _f = f;

    private const string Address = "ul. Testowa 1, 30-001 Kraków"; // z OrderScenario
    private const string Phone = "600100200";

    private sealed record BoardRow(Guid id, Guid orderId, string orderCode, string status, Guid? driverId, string? driverName,
        int? stopSequence, int version, string? deliveryDate, string? windowStart, string? windowEnd, string? address, string? phone);
    private sealed record Driver(Guid id, string fullName);
    private sealed record Board(List<BoardRow> deliveries, List<Driver> drivers);
    private sealed record Mine(Guid id, string status, int? stopSequence, int version, string? address, string? phone,
        string? windowStart);
    private sealed record Change(string action, string fromStatus, string toStatus, Guid? driverId, int? stopSequence, int version,
        string? actor, DateTime atUtc);
    private sealed record OrderDto(Guid id, string status);

    private Task<HttpResponseMessage> BoardRaw(string token, Guid storeId, string query = "")
        => _f.Authed(token).GetAsync($"/api/delivery/stores/{storeId}/board{query}");

    private async Task<Board> BoardAsync(string token, Guid storeId, string query = "")
    {
        var r = await BoardRaw(token, storeId, query);
        r.EnsureSuccessStatusCode();
        return (await r.Content.ReadFromJsonAsync<Board>())!;
    }

    private async Task<List<Mine>> MineAsync(string token)
        => (await _f.Authed(token).GetFromJsonAsync<List<Mine>>("/api/delivery/mine"))!;

    [Fact]
    public async Task Operator_sees_deliveries_with_contact_only_for_own_stores_including_multiple_locations()
    {
        var a = await OrderScenario.StoreWithSlotAsync(_f);
        var b = await OrderScenario.StoreWithSlotAsync(_f);
        var readyA = await DeliveryScenario.ReadyOrderAsync(_f, a);
        await DeliveryScenario.ReadyOrderAsync(_f, b);

        // Pracownik sklepu A z drugą lokalizacją (własny nowy sklep) — po odświeżeniu tokenu zarządza obiema.
        var emp = await _f.CreateEmployeeAsync(a.AdminToken, a.StoreId.ToString());
        (await _f.Authed(emp.accessToken).PostAsJsonAsync("/api/merchant/stores",
            new { name = $"Lok {Guid.NewGuid():N}"[..14], city = "Kraków", commissionRate = 0.10m, minimumOrderValue = 0m }))
            .EnsureSuccessStatusCode();
        var refreshed = (await (await _f.Anon().PostAsJsonAsync("/api/identity/refresh", new { refreshToken = emp.refreshToken }))
            .Content.ReadFromJsonAsync<AuthDto>())!;
        refreshed.user.storeIds.Should().HaveCount(2);

        var boardA = await BoardAsync(refreshed.accessToken, a.StoreId);
        var row = boardA.deliveries.Should().ContainSingle(d => d.id == readyA.DeliveryId).Subject;
        (row.address, row.phone, row.status, row.windowStart, row.windowEnd).Should().Be((Address, Phone, "AvailableForPickup", "18:00:00", "20:00:00"));
        (await BoardRaw(refreshed.accessToken, Guid.Parse(refreshed.user.storeIds.Single(s => s != a.StoreId.ToString())))).StatusCode
            .Should().Be(HttpStatusCode.OK, "druga lokalizacja pracownika");
        (await BoardRaw(refreshed.accessToken, b.StoreId)).StatusCode.Should().Be(HttpStatusCode.Forbidden, "obcy sklep");

        // Kierowca sklepu A i klient nie mają widoku operacyjnego; admin — tak (istniejące reguły).
        var driver = await _f.CreateStaffAsync(a.AdminToken, a.StoreId.ToString(), "Driver");
        (await BoardRaw(driver.accessToken, a.StoreId)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await BoardRaw(readyA.Customer.accessToken, a.StoreId)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await BoardRaw(a.AdminToken, b.StoreId)).StatusCode.Should().Be(HttpStatusCode.OK);

        // Filtry: dzień okna dostawy i status.
        var day = row.deliveryDate!;
        (await BoardAsync(a.AdminToken, a.StoreId, $"?date={day}")).deliveries.Should().Contain(d => d.id == readyA.DeliveryId);
        (await BoardAsync(a.AdminToken, a.StoreId, "?date=2001-01-01")).deliveries.Should().BeEmpty();
        (await BoardAsync(a.AdminToken, a.StoreId, "?status=Delivered")).deliveries.Should().NotContain(d => d.id == readyA.DeliveryId);
        (await BoardRaw(a.AdminToken, a.StoreId, "?status=bogus")).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await BoardAsync(a.AdminToken, a.StoreId)).drivers.Should().Contain(d => d.id == Guid.Parse(driver.user.id));
    }

    [Fact]
    public async Task Driver_gets_address_and_phone_only_after_this_delivery_is_assigned_to_them()
    {
        var s = await OrderScenario.StoreWithSlotAsync(_f);
        var ready = await DeliveryScenario.ReadyOrderAsync(_f, s);
        var d1 = await _f.CreateStaffAsync(s.AdminToken, s.StoreId.ToString(), "Driver");
        var d2 = await _f.CreateStaffAsync(s.AdminToken, s.StoreId.ToString(), "Driver");

        // Przed przypisaniem: brak danych klienta nigdzie (pula bez adresu, „moje" puste).
        (await _f.Authed(d1.accessToken).GetStringAsync("/api/delivery/available")).Should().NotContain("Testowa").And.NotContain(Phone);
        (await MineAsync(d1.accessToken)).Should().BeEmpty();

        (await DeliveryScenario.AssignAsync(_f, s.AdminToken, s.StoreId, ready.DeliveryId, Guid.Parse(d1.user.id))).EnsureSuccessStatusCode();

        var mine = (await MineAsync(d1.accessToken)).Should().ContainSingle().Subject;
        (mine.address, mine.phone, mine.status, mine.stopSequence, mine.windowStart).Should().Be((Address, Phone, "Assigned", (int?)1, "18:00:00"));
        (await _f.Authed(d2.accessToken).GetStringAsync("/api/delivery/mine")).Should().NotContain("Testowa").And.NotContain(Phone);

        // Dezaktywacja konta: stary token nadal ważny, ale dane klienta i akcje są niedostępne (stan z bazy).
        (await _f.Authed(s.AdminToken).PostAsJsonAsync($"/api/identity/admin/users/{d1.user.id}/active", new { isActive = false }))
            .EnsureSuccessStatusCode();
        (await _f.Authed(d1.accessToken).GetStringAsync("/api/delivery/mine")).Should().NotContain("Testowa").And.NotContain(Phone);
        (await _f.Authed(d1.accessToken).PostAsync($"/api/delivery/{ready.DeliveryId}/pick-up", null))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Driver_of_store_A_cannot_read_or_change_delivery_of_store_B()
    {
        var a = await OrderScenario.StoreWithSlotAsync(_f);
        var b = await OrderScenario.StoreWithSlotAsync(_f);
        var readyB = await DeliveryScenario.ReadyOrderAsync(_f, b);
        var driverA = await _f.CreateStaffAsync(a.AdminToken, a.StoreId.ToString(), "Driver");
        var driverB = await _f.CreateStaffAsync(b.AdminToken, b.StoreId.ToString(), "Driver");

        // Operator nie przypisze dostawy B kierowcy sklepu A.
        (await DeliveryScenario.AssignAsync(_f, b.AdminToken, b.StoreId, readyB.DeliveryId, Guid.Parse(driverA.user.id)))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await DeliveryScenario.AssignAsync(_f, b.AdminToken, b.StoreId, readyB.DeliveryId, Guid.Parse(driverB.user.id)))
            .EnsureSuccessStatusCode();

        var dac = _f.Authed(driverA.accessToken);
        (await dac.GetStringAsync("/api/delivery/mine")).Should().NotContain("Testowa");
        foreach (var action in new[] { "pick-up", "delivered" })
            (await dac.PostAsync($"/api/delivery/{readyB.DeliveryId}/{action}", null)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await dac.GetAsync($"/api/delivery/orders/{readyB.OrderId}")).StatusCode.Should().Be(HttpStatusCode.Forbidden);

        // Dane historyczne: dostawa B przypisana kierowcy A (bez przypisania do sklepu B) — nadal bez dostępu.
        using (var scope = _f.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DeliveryDbContext>();
            var d = await db.Deliveries.SingleAsync(x => x.Id == readyB.DeliveryId);
            db.Entry(d).Property(nameof(d.DriverId)).CurrentValue = Guid.Parse(driverA.user.id);
            await db.SaveChangesAsync();
        }
        (await dac.GetStringAsync("/api/delivery/mine")).Should().NotContain("Testowa").And.NotContain(Phone);
        (await dac.PostAsync($"/api/delivery/{readyB.DeliveryId}/pick-up", null)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await DeliveryScenario.DeliveryEventsAsync(_f, "OrderPickedUp", readyB.OrderId)).Should().Be(0);
    }

    [Fact]
    public async Task Two_parallel_assignments_of_one_delivery_assign_exactly_one_driver()
    {
        var s = await OrderScenario.StoreWithSlotAsync(_f);
        var ready = await DeliveryScenario.ReadyOrderAsync(_f, s);
        var emp = await _f.CreateEmployeeAsync(s.AdminToken, s.StoreId.ToString());
        var d1 = Guid.Parse((await _f.CreateStaffAsync(s.AdminToken, s.StoreId.ToString(), "Driver")).user.id);
        var d2 = Guid.Parse((await _f.CreateStaffAsync(s.AdminToken, s.StoreId.ToString(), "Driver")).user.id);

        var responses = await OrderScenario.FireTogetherAsync(new Func<Task<HttpResponseMessage>>[]
        {
            () => DeliveryScenario.AssignAsync(_f, s.AdminToken, s.StoreId, ready.DeliveryId, d1),
            () => DeliveryScenario.AssignAsync(_f, emp.accessToken, s.StoreId, ready.DeliveryId, d2),
        });

        responses.Count(r => r.IsSuccessStatusCode).Should().Be(1);
        responses.Where(r => !r.IsSuccessStatusCode).Should().OnlyContain(r => r.StatusCode == HttpStatusCode.Conflict);
        var row = (await BoardAsync(s.AdminToken, s.StoreId)).deliveries.Single(d => d.id == ready.DeliveryId);
        new[] { d1, d2 }.Should().Contain(row.driverId!.Value);
        var history = (await _f.Authed(s.AdminToken).GetFromJsonAsync<List<Change>>(
            $"/api/delivery/stores/{s.StoreId}/deliveries/{ready.DeliveryId}/history"))!;
        history.Should().ContainSingle(h => h.action == "assigned").Which.driverId.Should().Be(row.driverId);
    }

    [Fact]
    public async Task Route_order_is_saved_and_parallel_or_stale_reorders_never_mix()
    {
        var s = await OrderScenario.StoreWithSlotAsync(_f);
        var driver = Guid.Parse((await _f.CreateStaffAsync(s.AdminToken, s.StoreId.ToString(), "Driver")).user.id);
        var ids = new List<Guid>();
        for (var i = 0; i < 3; i++)
        {
            var r = await DeliveryScenario.ReadyOrderAsync(_f, s);
            (await DeliveryScenario.AssignAsync(_f, s.AdminToken, s.StoreId, r.DeliveryId, driver)).EnsureSuccessStatusCode();
            ids.Add(r.DeliveryId);
        }
        var route = (await BoardAsync(s.AdminToken, s.StoreId)).deliveries.Where(d => ids.Contains(d.id)).ToList();
        route.OrderBy(d => d.stopSequence).Select(d => d.id).Should().Equal(ids, "kolejne przypisania trafiają na koniec trasy");
        var ver = route.ToDictionary(d => d.id, d => d.version);
        var first = route[0];

        object Body(params Guid[] order) => new
        {
            driverId = driver, deliveryDate = first.deliveryDate, windowStart = first.windowStart, windowEnd = first.windowEnd,
            stops = order.Select(id => new { deliveryId = id, expectedVersion = ver[id] }),
        };
        var orderX = new[] { ids[2], ids[0], ids[1] };
        var orderY = new[] { ids[1], ids[2], ids[0] };
        var responses = await OrderScenario.FireTogetherAsync(new Func<Task<HttpResponseMessage>>[]
        {
            () => _f.Authed(s.AdminToken).PutAsJsonAsync($"/api/delivery/stores/{s.StoreId}/route", Body(orderX)),
            () => _f.Authed(s.AdminToken).PutAsJsonAsync($"/api/delivery/stores/{s.StoreId}/route", Body(orderY)),
        });
        responses.Count(r => r.IsSuccessStatusCode).Should().Be(1);
        responses.Single(r => !r.IsSuccessStatusCode).StatusCode.Should().Be(HttpStatusCode.Conflict);

        var winner = responses[0].IsSuccessStatusCode ? orderX : orderY;
        var saved = (await BoardAsync(s.AdminToken, s.StoreId)).deliveries.Where(d => ids.Contains(d.id))
            .OrderBy(d => d.stopSequence).Select(d => d.id).ToList();
        saved.Should().Equal(winner, "zapisana jest w całości jedna kolejność — nigdy mieszanka");

        // Nieaktualne wersje i niepełna trasa — odrzucone bez zmian.
        (await _f.Authed(s.AdminToken).PutAsJsonAsync($"/api/delivery/stores/{s.StoreId}/route", Body(orderX == winner ? orderY : orderX)))
            .StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await _f.Authed(s.AdminToken).PutAsJsonAsync($"/api/delivery/stores/{s.StoreId}/route", Body(ids[0], ids[1])))
            .StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await BoardAsync(s.AdminToken, s.StoreId)).deliveries.Where(d => ids.Contains(d.id))
            .OrderBy(d => d.stopSequence).Select(d => d.id).Should().Equal(winner);
    }

    [Fact]
    public async Task Invalid_transitions_create_no_events_and_valid_delivery_emits_OrderDelivered_exactly_once()
    {
        var s = await OrderScenario.StoreWithSlotAsync(_f);
        var ready = await DeliveryScenario.ReadyOrderAsync(_f, s);
        var driver = await _f.CreateStaffAsync(s.AdminToken, s.StoreId.ToString(), "Driver");
        var dc = _f.Authed(driver.accessToken);

        // Nieprzypisany kierowca: odmowa; przypisany — „dostarczono" przed odbiorem: 409. Żadnych zdarzeń.
        (await dc.PostAsync($"/api/delivery/{ready.DeliveryId}/pick-up", null)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await DeliveryScenario.AssignAsync(_f, s.AdminToken, s.StoreId, ready.DeliveryId, Guid.Parse(driver.user.id))).EnsureSuccessStatusCode();
        (await dc.PostAsync($"/api/delivery/{ready.DeliveryId}/delivered", null)).StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await DeliveryScenario.DeliveryEventsAsync(_f, "OrderDelivered", ready.OrderId)).Should().Be(0);
        (await DeliveryScenario.DeliveryEventsAsync(_f, "OrderPickedUp", ready.OrderId)).Should().Be(0);

        // Zamówienia nie da się oznaczyć jako dostarczone z pominięciem dostawy.
        (await _f.Authed(s.AdminToken).PostAsync($"/api/ordering/orders/{ready.OrderId}/delivered", null))
            .IsSuccessStatusCode.Should().BeFalse();

        (await dc.PostAsync($"/api/delivery/{ready.DeliveryId}/pick-up", null)).EnsureSuccessStatusCode();
        var responses = await OrderScenario.FireTogetherAsync(Enumerable.Range(0, 4).Select(_ => (Func<Task<HttpResponseMessage>>)(
            () => dc.PostAsync($"/api/delivery/{ready.DeliveryId}/delivered", null))).ToList());
        responses.Count(r => r.IsSuccessStatusCode).Should().Be(1);
        responses.Where(r => !r.IsSuccessStatusCode).Should().OnlyContain(r => r.StatusCode == HttpStatusCode.Conflict);
        (await DeliveryScenario.DeliveryEventsAsync(_f, "OrderPickedUp", ready.OrderId)).Should().Be(1);
        (await DeliveryScenario.DeliveryEventsAsync(_f, "OrderDelivered", ready.OrderId)).Should().Be(1);

        // Zdarzenie przesuwa zamówienie; historia zawiera autora i czas każdego kroku; adres znika z „moich".
        string status = "";
        for (var i = 0; i < 40 && status != "Delivered"; i++)
        {
            await DeliveryScenario.FlushOutboxAsync(_f);
            status = (await _f.Authed(s.AdminToken).GetFromJsonAsync<OrderDto>($"/api/ordering/orders/{ready.OrderId}"))!.status;
            if (status != "Delivered") await Task.Delay(100);
        }
        status.Should().Be("Delivered");
        var history = (await _f.Authed(s.AdminToken).GetFromJsonAsync<List<Change>>(
            $"/api/delivery/stores/{s.StoreId}/deliveries/{ready.DeliveryId}/history"))!;
        history.Select(h => h.action).Should().Equal("assigned", "picked_up", "delivered");
        history.Should().OnlyContain(h => h.actor != null && h.atUtc > DateTime.UtcNow.AddMinutes(-5));
        history.Skip(1).Should().OnlyContain(h => h.actor == driver.user.email);
        (await dc.GetStringAsync("/api/delivery/mine")).Should().NotContain("Testowa").And.NotContain(Phone);
    }

    private sealed record Snapshot(string Status, Guid? DriverId, int Version, int History, int DeliveryOutbox, int OrderingOutbox, string OrderStatus);

    private async Task<Snapshot> SnapshotAsync(string adminToken, Guid deliveryId, Guid orderId)
    {
        using var scope = _f.Services.CreateScope();
        var ddb = scope.ServiceProvider.GetRequiredService<DeliveryDbContext>();
        var odb = scope.ServiceProvider.GetRequiredService<ZipZap.Modules.Ordering.Infrastructure.OrderingDbContext>();
        var d = await ddb.Deliveries.AsNoTracking().SingleAsync(x => x.Id == deliveryId);
        var key = orderId.ToString();
        var order = (await _f.Authed(adminToken).GetFromJsonAsync<OrderDto>($"/api/ordering/orders/{orderId}"))!;
        return new Snapshot(d.Status.ToString(), d.DriverId, d.Version,
            await ddb.DeliveryChanges.CountAsync(c => c.DeliveryId == deliveryId),
            await ddb.OutboxMessages.CountAsync(m => m.Payload.Contains(key)),
            await odb.OutboxMessages.CountAsync(m => m.Payload.Contains(key)),
            order.status);
    }

    private async Task WaitForOrderStatusAsync(string adminToken, Guid orderId, string expected)
    {
        for (var i = 0; i < 50; i++)
        {
            await DeliveryScenario.FlushOutboxAsync(_f);
            if ((await _f.Authed(adminToken).GetFromJsonAsync<OrderDto>($"/api/ordering/orders/{orderId}"))!.status == expected) return;
            await Task.Delay(100);
        }
        throw new TimeoutException($"Zamówienie nie osiągnęło statusu {expected}.");
    }

    [Fact]
    public async Task Admin_cannot_use_driver_actions_on_someone_elses_or_unassigned_delivery()
    {
        var s = await OrderScenario.StoreWithSlotAsync(_f);
        var assigned = await DeliveryScenario.ReadyOrderAsync(_f, s);
        var unassigned = await DeliveryScenario.ReadyOrderAsync(_f, s);
        var driver = await _f.CreateStaffAsync(s.AdminToken, s.StoreId.ToString(), "Driver");
        (await DeliveryScenario.AssignAsync(_f, s.AdminToken, s.StoreId, assigned.DeliveryId, Guid.Parse(driver.user.id))).EnsureSuccessStatusCode();
        (await _f.Authed(driver.accessToken).PostAsync($"/api/delivery/{assigned.DeliveryId}/pick-up", null)).EnsureSuccessStatusCode();
        await WaitForOrderStatusAsync(s.AdminToken, assigned.OrderId, "InDelivery"); // legalny odbiór kierowcy przetworzony

        var before = new[]
        {
            await SnapshotAsync(s.AdminToken, assigned.DeliveryId, assigned.OrderId),
            await SnapshotAsync(s.AdminToken, unassigned.DeliveryId, unassigned.OrderId),
        };

        var admin = _f.Authed(s.AdminToken);
        foreach (var id in new[] { assigned.DeliveryId, unassigned.DeliveryId })
            foreach (var action in new[] { "pick-up", "delivered" })
                (await admin.PostAsync($"/api/delivery/{id}/{action}", null)).StatusCode
                    .Should().Be(HttpStatusCode.Forbidden, "admin nie jest przypisanym kierowcą ({0} {1})", action, id);

        await DeliveryScenario.FlushOutboxAsync(_f);
        (await SnapshotAsync(s.AdminToken, assigned.DeliveryId, assigned.OrderId)).Should().Be(before[0],
            "dostawa innego kierowcy: bez zmiany stanu, historii, zdarzeń i statusu zamówienia");
        (await SnapshotAsync(s.AdminToken, unassigned.DeliveryId, unassigned.OrderId)).Should().Be(before[1],
            "dostawa nieprzypisana: bez zmian");
        before[0].Status.Should().Be("InTransit");
        before[1].Status.Should().Be("AvailableForPickup");
    }

    [Fact]
    public async Task Admin_emergency_override_is_separate_requires_reason_is_audited_and_emits_once()
    {
        var s = await OrderScenario.StoreWithSlotAsync(_f);
        var ready = await DeliveryScenario.ReadyOrderAsync(_f, s);
        var idle = await DeliveryScenario.ReadyOrderAsync(_f, s);
        var driver = await _f.CreateStaffAsync(s.AdminToken, s.StoreId.ToString(), "Driver");
        var employee = await _f.CreateEmployeeAsync(s.AdminToken, s.StoreId.ToString());
        (await DeliveryScenario.AssignAsync(_f, s.AdminToken, s.StoreId, ready.DeliveryId, Guid.Parse(driver.user.id))).EnsureSuccessStatusCode();
        (await _f.Authed(driver.accessToken).PostAsync($"/api/delivery/{ready.DeliveryId}/pick-up", null)).EnsureSuccessStatusCode();
        var version = (await BoardAsync(s.AdminToken, s.StoreId)).deliveries.Single(d => d.id == ready.DeliveryId).version;
        const string reason = "Telefon kierowcy rozładowany — odbiór potwierdzony telefonicznie przez sklep";
        Task<HttpResponseMessage> Override(string token, Guid id, string action, string why, int ver) =>
            _f.Authed(token).PostAsJsonAsync($"/api/delivery/admin/deliveries/{id}/override",
                new { action, reason = why, expectedVersion = ver });

        (await Override(employee.accessToken, ready.DeliveryId, "delivered", reason, version)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await Override(driver.accessToken, ready.DeliveryId, "delivered", reason, version)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await Override(s.AdminToken, ready.DeliveryId, "delivered", "", version)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await Override(s.AdminToken, ready.DeliveryId, "delivered", "bo tak", version)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await Override(s.AdminToken, ready.DeliveryId, "delivered", reason, version - 1)).StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await DeliveryScenario.DeliveryEventsAsync(_f, "OrderDelivered", ready.OrderId)).Should().Be(0);

        (await Override(s.AdminToken, ready.DeliveryId, "delivered", reason, version)).EnsureSuccessStatusCode();
        (await Override(s.AdminToken, ready.DeliveryId, "delivered", reason, version + 1)).StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await DeliveryScenario.DeliveryEventsAsync(_f, "OrderDelivered", ready.OrderId)).Should().Be(1);
        var history = (await _f.Authed(s.AdminToken).GetFromJsonAsync<List<ChangeWithReason>>(
            $"/api/delivery/stores/{s.StoreId}/deliveries/{ready.DeliveryId}/history"))!;
        var last = history.Last();
        (last.action, last.toStatus, last.reason, last.actor).Should().Be(("override_delivered", "Delivered", reason, "admin@zipzap.local"));
        history.Take(history.Count - 1).Should().OnlyContain(h => h.reason == null);

        // Nie da się awaryjnie przeskoczyć przypisania: dostawa nieprzypisana → 409, bez zdarzeń.
        var idleVersion = (await BoardAsync(s.AdminToken, s.StoreId)).deliveries.Single(d => d.id == idle.DeliveryId).version;
        (await Override(s.AdminToken, idle.DeliveryId, "picked_up", reason, idleVersion)).StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await DeliveryScenario.DeliveryEventsAsync(_f, "OrderPickedUp", idle.OrderId)).Should().Be(0);
    }

    private sealed record ChangeWithReason(string action, string toStatus, string? actor, string? reason);

    [Fact]
    public async Task Driver_cannot_self_accept_from_the_pool_anymore()
    {
        var s = await OrderScenario.StoreWithSlotAsync(_f);
        var ready = await DeliveryScenario.ReadyOrderAsync(_f, s);
        var driver = await _f.CreateStaffAsync(s.AdminToken, s.StoreId.ToString(), "Driver");

        (await _f.Authed(driver.accessToken).PostAsync($"/api/delivery/{ready.DeliveryId}/accept", null))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await BoardAsync(s.AdminToken, s.StoreId)).deliveries.Single(d => d.id == ready.DeliveryId).driverId.Should().BeNull();
    }
}
