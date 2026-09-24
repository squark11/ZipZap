using System.Text.Json;
using ZipZap.BuildingBlocks.Domain;

namespace ZipZap.Modules.Ordering.Domain;

/// <summary>Godzina rundy zakupowej w czasie LOKALNYM strefy harmonogramu + termin graniczny.</summary>
public sealed record RoundTime(TimeOnly LocalTime, int CutoffMinutes);

/// <summary>
/// Harmonogram RUND ZAKUPOWYCH (kiedy operator kupuje/kompletuje w sklepie) — globalny dla platformy
/// w pilotażu jednego sklepu. To NIE jest harmonogram dostaw (terminy dostaw = <see cref="TimeSlot"/>)
/// ani <c>DeliveryWaves</c> (fale dostaw w ustawieniach platformy).
/// Godziny są lokalne w strefie <see cref="TimeZoneId"/>; konkretne wystąpienia liczymy do UTC per data
/// (odporne na zmianę czasu). Zapisany trwale w bazie (App_Data nie przeżywa redeployu).
/// </summary>
public sealed class PurchasingSchedule : Entity
{
    public static readonly Guid GlobalId = new("5c1a7e2d-0000-4000-8000-000000000001");
    public const string DefaultTimeZone = "Europe/Warsaw";
    /// <summary>Pon–Sob (bit = DayOfWeek). Niedziela wyłączona — ograniczenia handlu w niedziele; do potwierdzenia.</summary>
    public const int DefaultActiveDaysMask = 0b111_1110;
    public const string DefaultRoundsJson = """[{"LocalTime":"12:00:00","CutoffMinutes":30},{"LocalTime":"16:00:00","CutoffMinutes":30}]""";
    public const int DefaultMinDeliveryLeadMinutes = 60;

    public string TimeZoneId { get; private set; } = DefaultTimeZone;
    public string RoundsJson { get; private set; } = DefaultRoundsJson;
    public int ActiveDaysMask { get; private set; } = DefaultActiveDaysMask;
    /// <summary>Najwcześniejszy początek okna dostawy: tyle minut po starcie rundy (czas zakupów).</summary>
    public int MinDeliveryLeadMinutes { get; private set; } = DefaultMinDeliveryLeadMinutes;
    public DateTime UpdatedAtUtc { get; private set; }
    public Guid? UpdatedBy { get; private set; }

    private PurchasingSchedule() { } // EF

    /// <summary>Wartości startowe (te same co w seedzie migracji).</summary>
    public static PurchasingSchedule CreateDefault(DateTime nowUtc) => new() { Id = GlobalId, UpdatedAtUtc = nowUtc };

    public IReadOnlyList<RoundTime> Rounds =>
        (JsonSerializer.Deserialize<List<RoundTime>>(RoundsJson) ?? new())
            .OrderBy(r => r.LocalTime).ToList();

    public bool IsActiveDay(DayOfWeek day) => (ActiveDaysMask & (1 << (int)day)) != 0;

    public TimeZoneInfo ResolveTimeZone() => TimeZoneInfo.FindSystemTimeZoneById(TimeZoneId);

    /// <summary>Zmiana konfiguracji z walidacją (bez zapisu niepoprawnego harmonogramu).</summary>
    public void Update(string timeZoneId, IReadOnlyCollection<RoundTime> rounds, int activeDaysMask,
        int minDeliveryLeadMinutes, Guid? by, DateTime nowUtc)
    {
        if (string.IsNullOrWhiteSpace(timeZoneId))
            throw new OrderingDomainException("Podaj strefę czasową harmonogramu.");
        try { TimeZoneInfo.FindSystemTimeZoneById(timeZoneId.Trim()); }
        catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException)
        { throw new OrderingDomainException($"Nieznana strefa czasowa '{timeZoneId}'."); }

        if (rounds is null || rounds.Count == 0)
            throw new OrderingDomainException("Harmonogram musi mieć co najmniej jedną rundę.");
        if (rounds.Count > 12)
            throw new OrderingDomainException("Zbyt wiele rund w ciągu dnia (maks. 12).");
        if (rounds.Select(r => r.LocalTime).Distinct().Count() != rounds.Count)
            throw new OrderingDomainException("Godziny rund nie mogą się powtarzać.");
        if (rounds.Any(r => r.CutoffMinutes is < 0 or > 720))
            throw new OrderingDomainException("Termin graniczny musi mieścić się w zakresie 0–720 minut przed rundą.");
        if ((activeDaysMask & 0b111_1111) == 0)
            throw new OrderingDomainException("Wybierz co najmniej jeden dzień z rundami.");
        if (minDeliveryLeadMinutes is < 0 or > 1440)
            throw new OrderingDomainException("Czas od rundy do dostawy musi mieścić się w zakresie 0–1440 minut.");

        TimeZoneId = timeZoneId.Trim();
        RoundsJson = JsonSerializer.Serialize(rounds.OrderBy(r => r.LocalTime).ToList());
        ActiveDaysMask = activeDaysMask & 0b111_1111;
        MinDeliveryLeadMinutes = minDeliveryLeadMinutes;
        UpdatedBy = by;
        UpdatedAtUtc = nowUtc;
    }
}
