using Microsoft.EntityFrameworkCore;
using ZipZap.BuildingBlocks.Domain;
using ZipZap.BuildingBlocks.MultiTenancy;
using ZipZap.Modules.Ordering.Domain;
using ZipZap.Modules.Ordering.Infrastructure;

namespace ZipZap.Modules.Ordering.Application;

/// <summary>Runda w postaci do wyświetlenia (czas lokalny strefy harmonogramu) + chwile UTC.</summary>
public sealed record PurchasingRoundInfo(
    DateOnly LocalDate, TimeOnly LocalTime, DateOnly CutoffLocalDate, TimeOnly CutoffLocalTime,
    string TimeZone, DateTime StartsAtUtc, DateTime CutoffAtUtc)
{
    public static PurchasingRoundInfo From(DateOnly localDate, TimeOnly localTime,
        DateTime startsAtUtc, DateTime cutoffAtUtc, TimeZoneInfo tz, string tzId)
    {
        var cutoffLocal = TimeZoneInfo.ConvertTimeFromUtc(cutoffAtUtc, tz);
        return new(localDate, localTime, DateOnly.FromDateTime(cutoffLocal), TimeOnly.FromDateTime(cutoffLocal),
            tzId, startsAtUtc, cutoffAtUtc);
    }
}

/// <summary>Najwcześniejszy możliwy początek okna dostawy (po zakupach w rundzie).</summary>
public sealed record EarliestDeliveryInfo(DateOnly LocalDate, TimeOnly LocalTime, DateTime AtUtc);

/// <summary>Podgląd dla klienta PRZED złożeniem zamówienia.</summary>
public sealed record RoundPreviewDto(
    bool Available, string? Reason, string? Message,
    PurchasingRoundInfo? Round, EarliestDeliveryInfo? EarliestDelivery);

public sealed record RoundTimeDto(string LocalTime, int CutoffMinutes);

public sealed record PurchasingScheduleDto(
    string TimeZoneId, IReadOnlyList<RoundTimeDto> Rounds, IReadOnlyList<string> ActiveDays,
    int MinDeliveryLeadMinutes, DateTime UpdatedAtUtc);

/// <summary>Wynik rozstrzygnięcia rundy dla checkoutu (bez zapisu do bazy).</summary>
public sealed record RoundResolution(RoundOccurrence Occurrence, TimeZoneInfo TimeZone, string TimeZoneId, int MinDeliveryLeadMinutes)
{
    public DateTime EarliestDeliveryUtc => Occurrence.StartsAtUtc.AddMinutes(MinDeliveryLeadMinutes);
}

/// <summary>
/// Rundy zakupowe: podgląd najbliższej rundy (klient), rozstrzygnięcie i utrwalenie rundy przy
/// checkoucie, konfiguracja harmonogramu (admin). Czas bieżący z <see cref="TimeProvider"/> (testowalny).
/// </summary>
public sealed class PurchasingRoundService
{
    public const string ReasonStoreClosed = "store_closed";
    public const string ReasonNoRound = "no_round";

    private readonly OrderingDbContext _db;
    private readonly TimeProvider _clock;
    private readonly ICurrentUser _user;

    public PurchasingRoundService(OrderingDbContext db, TimeProvider clock, ICurrentUser user)
    {
        _db = db;
        _clock = clock;
        _user = user;
    }

    public DateTime UtcNow => _clock.GetUtcNow().UtcDateTime;

    private async Task<PurchasingSchedule> ScheduleAsync(CancellationToken ct)
    {
        var s = await _db.PurchasingSchedules.FirstOrDefaultAsync(x => x.Id == PurchasingSchedule.GlobalId, ct);
        if (s is not null) return s;
        // Wiersz jest seedowany migracją; gdyby zniknął — odtwarzamy wartości startowe TRWALE.
        s = PurchasingSchedule.CreateDefault(UtcNow);
        _db.PurchasingSchedules.Add(s);
        await _db.SaveChangesAsync(ct);
        return s;
    }

    /// <summary>Najbliższa runda dla sklepu (bez zapisu). Null = brak rundy w horyzoncie.</summary>
    public async Task<RoundResolution?> ResolveAsync(CancellationToken ct)
    {
        var schedule = await ScheduleAsync(ct);
        var tz = schedule.ResolveTimeZone();
        var occ = RoundCalculator.Next(schedule.Rounds, schedule.IsActiveDay, tz, UtcNow);
        return occ is null ? null : new RoundResolution(occ, tz, schedule.TimeZoneId, schedule.MinDeliveryLeadMinutes);
    }

    public async Task<Result<RoundPreviewDto>> PreviewAsync(Guid storeId, CancellationToken ct)
    {
        var store = await _db.CatalogStores.AsNoTracking().FirstOrDefaultAsync(s => s.Id == storeId, ct);
        if (store is null) return Error.NotFound("Sklep nie istnieje.");
        if (!store.IsAcceptingOrders)
            return new RoundPreviewDto(false, ReasonStoreClosed,
                "Sklep jest teraz zamknięty — nie przyjmuje zamówień.", null, null);

        var r = await ResolveAsync(ct);
        if (r is null)
            return new RoundPreviewDto(false, ReasonNoRound,
                "Brak dostępnej rundy zakupowej w najbliższych dniach.", null, null);

        var earliestLocal = TimeZoneInfo.ConvertTimeFromUtc(r.EarliestDeliveryUtc, r.TimeZone);
        return new RoundPreviewDto(true, null, null,
            PurchasingRoundInfo.From(r.Occurrence.LocalDate, r.Occurrence.LocalTime,
                r.Occurrence.StartsAtUtc, r.Occurrence.CutoffAtUtc, r.TimeZone, r.TimeZoneId),
            new EarliestDeliveryInfo(DateOnly.FromDateTime(earliestLocal), TimeOnly.FromDateTime(earliestLocal), r.EarliestDeliveryUtc));
    }

    /// <summary>
    /// Pobiera lub tworzy rundę sklepu dla danego wystąpienia (unikalne: sklep + start UTC).
    /// Równoległe checkouty tworzące tę samą rundę: przegrany insert czyta istniejący wiersz.
    /// </summary>
    public async Task<PurchasingRound> EnsureRoundAsync(Guid storeId, RoundResolution r, CancellationToken ct)
    {
        var o = r.Occurrence;
        var existing = await _db.PurchasingRounds.FirstOrDefaultAsync(
            x => x.StoreId == storeId && x.StartsAtUtc == o.StartsAtUtc, ct);
        if (existing is not null) return existing;

        var round = new PurchasingRound(storeId, o.LocalDate, o.LocalTime, r.TimeZoneId, o.StartsAtUtc, o.CutoffAtUtc, UtcNow);
        _db.PurchasingRounds.Add(round);
        try
        {
            await _db.SaveChangesAsync(ct);
            return round;
        }
        catch (DbUpdateException ex) when (ex.InnerException is Npgsql.PostgresException { SqlState: "23505" })
        {
            _db.Entry(round).State = EntityState.Detached;
            return await _db.PurchasingRounds.FirstAsync(x => x.StoreId == storeId && x.StartsAtUtc == o.StartsAtUtc, ct);
        }
    }

    public async Task<PurchasingRoundInfo?> InfoAsync(Guid? roundId, CancellationToken ct)
    {
        if (roundId is not Guid id) return null;
        var round = await _db.PurchasingRounds.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        return round is null ? null : ToInfo(round);
    }

    public static PurchasingRoundInfo ToInfo(PurchasingRound round)
    {
        var tz = TimeZoneInfo.FindSystemTimeZoneById(round.TimeZoneId);
        return PurchasingRoundInfo.From(round.LocalDate, round.LocalTime, round.StartsAtUtc, round.CutoffAtUtc, tz, round.TimeZoneId);
    }

    // ---- Konfiguracja (admin) ----

    public async Task<PurchasingScheduleDto> GetScheduleAsync(CancellationToken ct) => ToDto(await ScheduleAsync(ct));

    public async Task<Result<PurchasingScheduleDto>> UpdateScheduleAsync(PurchasingScheduleDto dto, CancellationToken ct)
    {
        var schedule = await ScheduleAsync(ct);
        var rounds = new List<RoundTime>();
        foreach (var r in dto.Rounds ?? Array.Empty<RoundTimeDto>())
        {
            if (!TimeOnly.TryParse(r.LocalTime, System.Globalization.CultureInfo.InvariantCulture, out var t))
                return Error.Validation($"Niepoprawna godzina rundy '{r.LocalTime}' (format HH:mm).");
            rounds.Add(new RoundTime(t, r.CutoffMinutes));
        }
        var mask = 0;
        foreach (var d in dto.ActiveDays ?? Array.Empty<string>())
        {
            // Tylko nazwy dni (TryParse przyjąłby też liczby, np. "99").
            if (!Enum.TryParse<DayOfWeek>(d, ignoreCase: true, out var day) || !Enum.IsDefined(day) || int.TryParse(d, out _))
                return Error.Validation($"Nieznany dzień tygodnia '{d}'.");
            mask |= 1 << (int)day;
        }
        try
        {
            schedule.Update(dto.TimeZoneId, rounds, mask, dto.MinDeliveryLeadMinutes, _user.UserId, UtcNow);
        }
        catch (OrderingDomainException ex)
        {
            return Error.Validation(ex.Message);
        }
        await _db.SaveChangesAsync(ct);
        return ToDto(schedule);
    }

    private static PurchasingScheduleDto ToDto(PurchasingSchedule s) => new(
        s.TimeZoneId,
        s.Rounds.Select(r => new RoundTimeDto(r.LocalTime.ToString("HH:mm"), r.CutoffMinutes)).ToList(),
        Enum.GetValues<DayOfWeek>().Where(s.IsActiveDay).Select(d => d.ToString()).ToList(),
        s.MinDeliveryLeadMinutes,
        s.UpdatedAtUtc);
}
