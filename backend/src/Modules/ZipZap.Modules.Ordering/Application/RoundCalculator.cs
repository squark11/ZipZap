using ZipZap.Modules.Ordering.Domain;

namespace ZipZap.Modules.Ordering.Application;

/// <summary>Wystąpienie rundy: lokalna data/godzina w strefie harmonogramu + chwile w UTC.</summary>
public sealed record RoundOccurrence(
    DateOnly LocalDate, TimeOnly LocalTime, int CutoffMinutes, DateTime StartsAtUtc, DateTime CutoffAtUtc);

/// <summary>
/// Czysta logika rund (bez bazy) — odporna na zmianę czasu:
/// <list type="bullet">
/// <item>godzina lokalna jest przeliczana na UTC osobno dla KAŻDEJ daty (inny offset latem/zimą),</item>
/// <item>godzina nieistniejąca (przeskok wiosenny) → runda tego dnia jest pomijana,</item>
/// <item>godzina podwójna (cofnięcie jesienne) → wybieramy WCZEŚNIEJSZE wystąpienie (ostrożniej dla klienta),</item>
/// <item>termin graniczny = N minut przed rzeczywistą chwilą rundy (liczony w UTC).</item>
/// </list>
/// </summary>
public static class RoundCalculator
{
    /// <summary>
    /// Najbliższa runda, której termin graniczny jeszcze nie minął (cutoff &gt; teraz), w dniach aktywnych,
    /// najdalej <paramref name="maxDaysAhead"/> dni naprzód. Null = brak rundy w horyzoncie.
    /// </summary>
    public static RoundOccurrence? Next(
        IReadOnlyList<RoundTime> rounds, Func<DayOfWeek, bool> isActiveDay, TimeZoneInfo tz,
        DateTime nowUtc, int maxDaysAhead = 14)
    {
        if (nowUtc.Kind != DateTimeKind.Utc) nowUtc = DateTime.SpecifyKind(nowUtc, DateTimeKind.Utc);
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(nowUtc, tz));
        var ordered = rounds.OrderBy(r => r.LocalTime).ToList();

        for (var d = 0; d <= maxDaysAhead; d++)
        {
            var date = today.AddDays(d);
            if (!isActiveDay(date.DayOfWeek)) continue;
            foreach (var r in ordered)
            {
                if (ToUtc(date, r.LocalTime, tz) is not DateTime startUtc) continue;
                var cutoffUtc = startUtc.AddMinutes(-r.CutoffMinutes);
                if (cutoffUtc > nowUtc)
                    return new RoundOccurrence(date, r.LocalTime, r.CutoffMinutes, startUtc, cutoffUtc);
            }
        }
        return null;
    }

    /// <summary>Lokalna data+godzina w strefie → UTC (null, gdy taka godzina nie istnieje tego dnia).</summary>
    public static DateTime? ToUtc(DateOnly date, TimeOnly time, TimeZoneInfo tz)
    {
        var local = date.ToDateTime(time, DateTimeKind.Unspecified);
        if (tz.IsInvalidTime(local)) return null;
        if (tz.IsAmbiguousTime(local))
        {
            // Dwa możliwe offsety — wcześniejsza chwila UTC odpowiada WIĘKSZEMU offsetowi (czas letni).
            var offset = tz.GetAmbiguousTimeOffsets(local).Max();
            return DateTime.SpecifyKind(local - offset, DateTimeKind.Utc);
        }
        return TimeZoneInfo.ConvertTimeToUtc(local, tz);
    }
}
