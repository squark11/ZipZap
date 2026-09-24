using FluentAssertions;
using Xunit;
using ZipZap.Modules.Ordering.Application;
using ZipZap.Modules.Ordering.Domain;

namespace ZipZap.Modules.Ordering.Tests;

/// <summary>
/// Rundy liczone w Europe/Warsaw z przeliczeniem na UTC per data. Zmiany czasu w 2026:
/// 29.03 02:00→03:00 (CET→CEST) i 25.10 03:00→02:00 (CEST→CET).
/// </summary>
public class RoundCalculatorTests
{
    private static readonly TimeZoneInfo Warsaw = TimeZoneInfo.FindSystemTimeZoneById("Europe/Warsaw");
    private static readonly RoundTime[] Default = { new(new TimeOnly(12, 0), 30), new(new TimeOnly(16, 0), 30) };
    private static bool MonToSat(DayOfWeek d) => d != DayOfWeek.Sunday;
    private static bool Always(DayOfWeek _) => true;
    private static DateTime U(int y, int mo, int d, int h, int mi) => new(y, mo, d, h, mi, 0, DateTimeKind.Utc);

    [Fact]
    public void Summer_noon_is_10_utc_and_cutoff_30_min_before()
    {
        var r = RoundCalculator.Next(Default, MonToSat, Warsaw, U(2026, 7, 1, 8, 0))!; // śr 10:00 CEST
        r.LocalDate.Should().Be(new DateOnly(2026, 7, 1));
        r.LocalTime.Should().Be(new TimeOnly(12, 0));
        r.StartsAtUtc.Should().Be(U(2026, 7, 1, 10, 0));
        r.CutoffAtUtc.Should().Be(U(2026, 7, 1, 9, 30));
    }

    [Fact]
    public void Winter_noon_is_11_utc_same_local_hour_different_offset()
    {
        var r = RoundCalculator.Next(Default, MonToSat, Warsaw, U(2026, 1, 14, 8, 0))!; // śr 09:00 CET
        r.LocalTime.Should().Be(new TimeOnly(12, 0));
        r.StartsAtUtc.Should().Be(U(2026, 1, 14, 11, 0));
    }

    [Fact]
    public void Exactly_at_cutoff_the_round_is_closed()
    {
        var r = RoundCalculator.Next(Default, MonToSat, Warsaw, U(2026, 7, 1, 9, 30))!; // 11:30 = cutoff 12:00
        r.LocalTime.Should().Be(new TimeOnly(16, 0));
        r.StartsAtUtc.Should().Be(U(2026, 7, 1, 14, 0));
    }

    [Fact]
    public void After_last_cutoff_moves_to_next_day()
    {
        var r = RoundCalculator.Next(Default, MonToSat, Warsaw, U(2026, 7, 1, 13, 31))!; // śr 15:31
        r.LocalDate.Should().Be(new DateOnly(2026, 7, 2));
        r.StartsAtUtc.Should().Be(U(2026, 7, 2, 10, 0));
    }

    [Fact]
    public void Saturday_evening_skips_inactive_sunday_to_monday()
    {
        var r = RoundCalculator.Next(Default, MonToSat, Warsaw, U(2026, 7, 4, 14, 0))!; // sob 16:00
        r.LocalDate.Should().Be(new DateOnly(2026, 7, 6));
        r.LocalDate.DayOfWeek.Should().Be(DayOfWeek.Monday);
    }

    [Fact]
    public void Spring_forward_day_uses_the_new_offset_of_that_date()
    {
        // 29.03.2026, 01:30 CET (przed przestawieniem) → runda 12:00 tego dnia jest już w CEST = 10:00 UTC.
        var r = RoundCalculator.Next(Default, Always, Warsaw, U(2026, 3, 29, 0, 30))!;
        r.LocalDate.Should().Be(new DateOnly(2026, 3, 29));
        r.StartsAtUtc.Should().Be(U(2026, 3, 29, 10, 0));
    }

    [Fact]
    public void Fall_back_day_uses_the_winter_offset_after_change()
    {
        // 25.10.2026, 00:00 CEST → runda 12:00 tego dnia jest już w CET = 11:00 UTC.
        var r = RoundCalculator.Next(Default, Always, Warsaw, U(2026, 10, 24, 22, 0))!;
        r.LocalDate.Should().Be(new DateOnly(2026, 10, 25));
        r.StartsAtUtc.Should().Be(U(2026, 10, 25, 11, 0));
    }

    [Fact]
    public void Nonexistent_local_time_on_spring_day_is_skipped()
    {
        var at0230 = new[] { new RoundTime(new TimeOnly(2, 30), 0) };
        var r = RoundCalculator.Next(at0230, Always, Warsaw, U(2026, 3, 28, 23, 0))!; // 29.03 00:00 CET
        r.LocalDate.Should().Be(new DateOnly(2026, 3, 30), "02:30 nie istnieje 29.03");
        r.StartsAtUtc.Should().Be(U(2026, 3, 30, 0, 30)); // 02:30 CEST
    }

    [Fact]
    public void Ambiguous_local_time_on_fall_day_takes_the_earlier_instant()
    {
        var at0230 = new[] { new RoundTime(new TimeOnly(2, 30), 0) };
        var r = RoundCalculator.Next(at0230, Always, Warsaw, U(2026, 10, 24, 22, 0))!; // 25.10 00:00 CEST
        r.LocalDate.Should().Be(new DateOnly(2026, 10, 25));
        r.StartsAtUtc.Should().Be(U(2026, 10, 25, 0, 30), "pierwsze wystąpienie 02:30 (jeszcze CEST)");
    }

    [Fact]
    public void No_active_day_in_horizon_returns_null()
        => RoundCalculator.Next(Default, _ => false, Warsaw, U(2026, 7, 1, 8, 0)).Should().BeNull();

    [Fact]
    public void Schedule_rejects_duplicate_times()
    {
        var s = PurchasingSchedule.CreateDefault(DateTime.UtcNow);
        var act = () => s.Update("Europe/Warsaw",
            new[] { new RoundTime(new TimeOnly(12, 0), 30), new RoundTime(new TimeOnly(12, 0), 30) },
            PurchasingSchedule.DefaultActiveDaysMask, 60, null, DateTime.UtcNow);
        act.Should().Throw<OrderingDomainException>();
    }

    [Fact]
    public void Schedule_rejects_invalid_values_and_keeps_previous_config()
    {
        var s = PurchasingSchedule.CreateDefault(DateTime.UtcNow);
        var ok = new[] { new RoundTime(new TimeOnly(12, 0), 30) };

        FluentActions.Invoking(() => s.Update("Mars/Olympus", ok, 126, 60, null, DateTime.UtcNow)).Should().Throw<OrderingDomainException>();
        FluentActions.Invoking(() => s.Update("Europe/Warsaw", ok, 0, 60, null, DateTime.UtcNow)).Should().Throw<OrderingDomainException>();
        FluentActions.Invoking(() => s.Update("Europe/Warsaw", new[] { new RoundTime(new TimeOnly(12, 0), -5) }, 126, 60, null, DateTime.UtcNow))
            .Should().Throw<OrderingDomainException>();

        s.Rounds.Select(r => r.LocalTime).Should().Equal(new TimeOnly(12, 0), new TimeOnly(16, 0));
    }
}
