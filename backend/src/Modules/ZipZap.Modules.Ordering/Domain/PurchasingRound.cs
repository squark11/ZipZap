using ZipZap.BuildingBlocks.Domain;

namespace ZipZap.Modules.Ordering.Domain;

public enum PurchasingRoundStatus { Planned }

/// <summary>
/// Konkretna runda zakupowa sklepu (np. „pt 26.09, 12:00 Europe/Warsaw"). Chwile przechowujemy w UTC;
/// data i godzina lokalna + strefa są zapisane jako migawka (zmiana harmonogramu nie przepisuje
/// istniejących rund). Unikalna para (StoreId, StartsAtUtc). Kompletacja/statusy pozycji — S1b.
/// </summary>
public sealed class PurchasingRound : Entity
{
    public Guid StoreId { get; private set; }
    public DateOnly LocalDate { get; private set; }
    public TimeOnly LocalTime { get; private set; }
    public string TimeZoneId { get; private set; } = default!;
    public DateTime StartsAtUtc { get; private set; }
    public DateTime CutoffAtUtc { get; private set; }
    public PurchasingRoundStatus Status { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    private PurchasingRound() { } // EF

    public PurchasingRound(Guid storeId, DateOnly localDate, TimeOnly localTime, string timeZoneId,
        DateTime startsAtUtc, DateTime cutoffAtUtc, DateTime nowUtc)
    {
        if (startsAtUtc.Kind != DateTimeKind.Utc || cutoffAtUtc.Kind != DateTimeKind.Utc)
            throw new OrderingDomainException("Chwile rundy muszą być w UTC.");
        if (cutoffAtUtc > startsAtUtc)
            throw new OrderingDomainException("Termin graniczny nie może być po starcie rundy.");
        Id = Guid.NewGuid();
        StoreId = storeId;
        LocalDate = localDate;
        LocalTime = localTime;
        TimeZoneId = timeZoneId;
        StartsAtUtc = startsAtUtc;
        CutoffAtUtc = cutoffAtUtc;
        Status = PurchasingRoundStatus.Planned;
        CreatedAtUtc = nowUtc;
    }
}
