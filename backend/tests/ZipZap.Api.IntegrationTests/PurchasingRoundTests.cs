using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using ZipZap.Modules.Ordering.Infrastructure;

namespace ZipZap.Api.IntegrationTests;

/// <summary>
/// Rundy zakupowe 12:00/16:00 Europe/Warsaw (domyślny cutoff 30 min, pon–sob, dostawa ≥ 60 min po rundzie).
/// Stały zegar: środa 3.04.2030 (czas letni, UTC+2) — 12:00 lokalnie = 10:00 UTC.
/// </summary>
[Collection("rounds")]
public sealed class PurchasingRoundTests
{
    private readonly RoundsApiFactory _f;
    public PurchasingRoundTests(RoundsApiFactory f) => _f = f;

    private static readonly DateOnly Wed = new(2030, 4, 3);
    private static DateTime Utc(int day, int h, int m) => new(2030, 4, day, h, m, 0, DateTimeKind.Utc);

    private sealed record RoundInfo(string localDate, string localTime, string cutoffLocalDate, string cutoffLocalTime,
        string timeZone, DateTime startsAtUtc, DateTime cutoffAtUtc);
    private sealed record Earliest(string localDate, string localTime, DateTime atUtc);
    private sealed record Preview(bool available, string? reason, string? message, RoundInfo? round, Earliest? earliestDelivery);
    private sealed record OrderDto(Guid id, RoundInfo? purchasingRound);
    private sealed record RoundTime(string localTime, int cutoffMinutes);
    private sealed record Schedule(string timeZoneId, List<RoundTime> rounds, List<string> activeDays,
        int minDeliveryLeadMinutes, DateTime updatedAtUtc);

    private async Task<Preview> PreviewAsync(Guid storeId)
        => (await _f.Anon().GetFromJsonAsync<Preview>($"/api/ordering/stores/{storeId}/purchasing-round"))!;

    [Fact]
    public async Task Preview_shows_round_cutoff_and_earliest_delivery_in_local_time()
    {
        _f.Clock.Set(Utc(3, 8, 0)); // 10:00 lokalnie
        var s = await OrderScenario.StoreWithSlotAsync(_f, slotDate: Wed);

        var p = await PreviewAsync(s.StoreId);

        p.available.Should().BeTrue();
        p.round!.localDate.Should().Be("2030-04-03");
        p.round.localTime.Should().Be("12:00:00");
        p.round.cutoffLocalTime.Should().Be("11:30:00");
        p.round.timeZone.Should().Be("Europe/Warsaw");
        p.round.startsAtUtc.Should().Be(Utc(3, 10, 0));
        p.round.cutoffAtUtc.Should().Be(Utc(3, 9, 30));
        p.earliestDelivery!.localTime.Should().Be("13:00:00");
    }

    [Fact]
    public async Task After_cutoff_the_next_round_is_offered()
    {
        _f.Clock.Set(Utc(3, 9, 31)); // 11:31 lokalnie — po terminie granicznym 12:00
        var s = await OrderScenario.StoreWithSlotAsync(_f, slotDate: Wed);

        var p = await PreviewAsync(s.StoreId);

        p.round!.localTime.Should().Be("16:00:00");
        p.round.startsAtUtc.Should().Be(Utc(3, 14, 0));
    }

    [Fact]
    public async Task Saturday_after_last_cutoff_moves_to_monday_skipping_sunday()
    {
        _f.Clock.Set(Utc(6, 13, 31)); // sobota 15:31 lokalnie
        var s = await OrderScenario.StoreWithSlotAsync(_f, slotDate: new DateOnly(2030, 4, 8));

        var p = await PreviewAsync(s.StoreId);

        p.round!.localDate.Should().Be("2030-04-08"); // poniedziałek
        p.round.localTime.Should().Be("12:00:00");
        p.round.startsAtUtc.Should().Be(Utc(8, 10, 0));
    }

    [Fact]
    public async Task Closed_store_gets_no_round_and_a_clear_message()
    {
        _f.Clock.Set(Utc(3, 8, 0));
        var s = await OrderScenario.StoreWithSlotAsync(_f, slotDate: Wed, storeStatus: "Closed");

        var p = await PreviewAsync(s.StoreId);

        p.available.Should().BeFalse();
        p.reason.Should().Be("store_closed");
        p.message.Should().Contain("zamknięty");
        p.round.Should().BeNull();
    }

    [Fact]
    public async Task Checkout_assigns_orders_to_the_same_persisted_round()
    {
        _f.Clock.Set(Utc(3, 8, 0));
        var s = await OrderScenario.StoreWithSlotAsync(_f, slotDate: Wed);
        var c1 = await _f.RegisterCustomerAsync();
        var c2 = await _f.RegisterCustomerAsync();

        var o1 = (await (await OrderScenario.CheckoutAsync(_f, s, c1.accessToken)).EnsureSuccessStatusCode()
            .Content.ReadFromJsonAsync<OrderDto>())!;
        var o2 = (await (await OrderScenario.CheckoutAsync(_f, s, c2.accessToken)).EnsureSuccessStatusCode()
            .Content.ReadFromJsonAsync<OrderDto>())!;

        o1.purchasingRound!.localTime.Should().Be("12:00:00");
        o1.purchasingRound.startsAtUtc.Should().Be(Utc(3, 10, 0));

        using var scope = _f.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OrderingDbContext>();
        (await db.PurchasingRounds.CountAsync(r => r.StoreId == s.StoreId)).Should().Be(1);
        var roundIds = await db.Orders.Where(o => o.Id == o1.id || o.Id == o2.id)
            .Select(o => o.PurchasingRoundId).Distinct().ToListAsync();
        roundIds.Should().ContainSingle().Which.Should().NotBeNull();
    }

    [Fact]
    public async Task Stale_expected_round_is_rejected_instead_of_silently_moving_the_order()
    {
        _f.Clock.Set(Utc(3, 9, 31)); // klient widział rundę 12:00, ale termin graniczny już minął
        var s = await OrderScenario.StoreWithSlotAsync(_f, slotDate: Wed);
        var c = await _f.RegisterCustomerAsync();

        var stale = await OrderScenario.CheckoutAsync(_f, s, c.accessToken, expectedRoundStartsAtUtc: Utc(3, 10, 0));
        stale.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await stale.Content.ReadAsStringAsync()).Should().Contain("16:00");

        var ok = await OrderScenario.CheckoutAsync(_f, s, c.accessToken, expectedRoundStartsAtUtc: Utc(3, 14, 0));
        ok.EnsureSuccessStatusCode();
        (await ok.Content.ReadFromJsonAsync<OrderDto>())!.purchasingRound!.localTime.Should().Be("16:00:00");
    }

    [Fact]
    public async Task Delivery_window_must_start_after_round_plus_lead_time()
    {
        _f.Clock.Set(Utc(3, 8, 0)); // runda 12:00 → dostawa najwcześniej 13:00 lokalnie
        var s = await OrderScenario.StoreWithSlotAsync(_f, slotDate: Wed, startTime: "12:00:00", endTime: "14:00:00");
        var c = await _f.RegisterCustomerAsync();

        var tooEarly = await OrderScenario.CheckoutAsync(_f, s, c.accessToken);
        tooEarly.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await tooEarly.Content.ReadAsStringAsync()).Should().Contain("przed zakupami w rundzie");

        var fine = await OrderScenario.AddSlotAsync(_f, s, Wed, "13:00:00", "15:00:00");
        (await OrderScenario.CheckoutAsync(_f, s, c.accessToken, slotId: fine)).EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Admin_changes_cutoff_persistently_and_it_drives_the_preview()
    {
        _f.Clock.Set(Utc(3, 8, 0));
        var admin = await _f.LoginAdminAsync();
        var ac = _f.Authed(admin.accessToken);
        var original = (await ac.GetFromJsonAsync<Schedule>("/api/ordering/admin/purchasing-schedule"))!;
        original.rounds.Select(r => r.localTime).Should().Equal("12:00", "16:00");
        original.timeZoneId.Should().Be("Europe/Warsaw");

        try
        {
            var changed = original with { rounds = new() { new("12:00", 45), new("16:00", 45) } };
            (await ac.PutAsJsonAsync("/api/ordering/admin/purchasing-schedule", changed)).EnsureSuccessStatusCode();

            var s = await OrderScenario.StoreWithSlotAsync(_f, slotDate: Wed);
            (await PreviewAsync(s.StoreId)).round!.cutoffLocalTime.Should().Be("11:15:00");

            // Trwałość: nowy host (restart procesu) czyta ten sam harmonogram z bazy.
            using var restarted = _f.WithWebHostBuilder(_ => { });
            var rc = restarted.CreateClient();
            rc.DefaultRequestHeaders.Authorization = new("Bearer", admin.accessToken);
            (await rc.GetFromJsonAsync<Schedule>("/api/ordering/admin/purchasing-schedule"))!
                .rounds.Should().OnlyContain(r => r.cutoffMinutes == 45);
        }
        finally
        {
            (await ac.PutAsJsonAsync("/api/ordering/admin/purchasing-schedule", original)).EnsureSuccessStatusCode();
        }
    }

    [Fact]
    public async Task Schedule_changes_are_admin_only_and_validated()
    {
        var admin = await _f.LoginAdminAsync();
        var current = (await _f.Authed(admin.accessToken).GetFromJsonAsync<Schedule>("/api/ordering/admin/purchasing-schedule"))!;
        var customer = await _f.RegisterCustomerAsync();

        (await _f.Authed(customer.accessToken).PutAsJsonAsync("/api/ordering/admin/purchasing-schedule", current))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var duplicate = current with { rounds = new() { new("12:00", 30), new("12:00", 30) } };
        (await _f.Authed(admin.accessToken).PutAsJsonAsync("/api/ordering/admin/purchasing-schedule", duplicate))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var badZone = current with { timeZoneId = "Mars/Olympus" };
        (await _f.Authed(admin.accessToken).PutAsJsonAsync("/api/ordering/admin/purchasing-schedule", badZone))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var numericDay = current with { activeDays = new() { "99" } };
        (await _f.Authed(admin.accessToken).PutAsJsonAsync("/api/ordering/admin/purchasing-schedule", numericDay))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
