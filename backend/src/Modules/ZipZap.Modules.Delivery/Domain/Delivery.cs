using ZipZap.BuildingBlocks.Domain;

namespace ZipZap.Modules.Delivery.Domain;

public enum DeliveryStatus { AvailableForPickup, Assigned, InTransit, Delivered }

public sealed class DeliveryDomainException : Exception
{
    public DeliveryDomainException(string message) : base(message) { }
}

/// <summary>
/// Dostawa zamówienia. Przepływ: dostępna (nieprzypisana) → przypisana kierowcy → w drodze → dostarczona.
/// Przydział i kolejność przystanków ustala operator sklepu (S1c). Okno dostawy to migawka wyboru klienta —
/// NIE jest tu zmieniane. Dane osobowe (adres/telefon) NIE są przechowywane w module dostaw.
/// Każda zmiana podbija <see cref="Version"/> i zwraca wpis historii (kto, kiedy, co).
/// </summary>
public sealed class Delivery : Entity
{
    public Guid OrderId { get; private set; }
    public Guid StoreId { get; private set; }
    public Guid? DriverId { get; private set; }
    public DeliveryStatus Status { get; private set; }
    public DateTime CreatedAtUtc { get; private set; } = DateTime.UtcNow;
    public DateTime? AssignedAtUtc { get; private set; }
    public DateTime? PickedUpAtUtc { get; private set; }
    public DateTime? DeliveredAtUtc { get; private set; }

    /// <summary>Okno dostawy wybrane przez klienta (data + godziny lokalne sklepu); null dla starszych dostaw.</summary>
    public DateOnly? DeliveryDate { get; private set; }
    public TimeOnly? WindowStart { get; private set; }
    public TimeOnly? WindowEnd { get; private set; }

    /// <summary>Kolejność przystanku w trasie kierowcy dla danego okna (ustala operator).</summary>
    public int? StopSequence { get; private set; }
    public Guid? AssignedBy { get; private set; }

    /// <summary>Wersja widziana przez operatora/kierowcę — zapis ze starszą wersją jest odrzucany.</summary>
    public int Version { get; private set; }

    private Delivery() { } // EF

    public Delivery(Guid orderId, Guid storeId, DateOnly? deliveryDate = null, TimeOnly? windowStart = null, TimeOnly? windowEnd = null)
    {
        OrderId = orderId;
        StoreId = storeId;
        Status = DeliveryStatus.AvailableForPickup;
        DeliveryDate = deliveryDate;
        WindowStart = windowStart;
        WindowEnd = windowEnd;
    }

    public bool IsOwnedBy(Guid driverId) => DriverId == driverId;

    /// <summary>Czy dostawa należy do tej samej trasy (kierowca + dzień + okno).</summary>
    public bool OnRoute(Guid driverId, DateOnly? date, TimeOnly? start, TimeOnly? end) =>
        DriverId == driverId && DeliveryDate == date && WindowStart == start && WindowEnd == end
        && Status is DeliveryStatus.Assigned or DeliveryStatus.InTransit;

    /// <summary>Operator przypisuje (lub przepina) dostawę kierowcy — tylko przed odbiorem.</summary>
    public DeliveryChange AssignTo(Guid driverId, int stopSequence, Guid by, string? byLabel, DateTime nowUtc)
    {
        if (Status is not (DeliveryStatus.AvailableForPickup or DeliveryStatus.Assigned))
            throw new DeliveryDomainException("Dostawę można przypisać tylko przed odbiorem przez kierowcę.");
        var previous = DriverId;
        var from = Status;
        DriverId = driverId;
        Status = DeliveryStatus.Assigned;
        AssignedAtUtc = nowUtc;
        AssignedBy = by;
        StopSequence = stopSequence;
        return Changed(DeliveryChange.Assigned, from, previous, by, byLabel, nowUtc);
    }

    /// <summary>Operator zdejmuje przypisanie (przed odbiorem) — dostawa wraca do nieprzypisanych.</summary>
    public DeliveryChange Unassign(Guid by, string? byLabel, DateTime nowUtc)
    {
        if (Status != DeliveryStatus.Assigned)
            throw new DeliveryDomainException("Przypisanie można zdjąć tylko przed odbiorem przez kierowcę.");
        var previous = DriverId;
        DriverId = null;
        Status = DeliveryStatus.AvailableForPickup;
        AssignedAtUtc = null;
        AssignedBy = null;
        StopSequence = null;
        return Changed(DeliveryChange.Unassigned, DeliveryStatus.Assigned, previous, by, byLabel, nowUtc);
    }

    /// <summary>Operator ustala pozycję przystanku w trasie.</summary>
    public DeliveryChange SetStop(int stopSequence, Guid by, string? byLabel, DateTime nowUtc)
    {
        if (Status is not (DeliveryStatus.Assigned or DeliveryStatus.InTransit) || DriverId is null)
            throw new DeliveryDomainException("Kolejność ustala się tylko dla przypisanych dostaw.");
        if (stopSequence < 1) throw new DeliveryDomainException("Numer przystanku musi być dodatni.");
        StopSequence = stopSequence;
        return Changed(DeliveryChange.Reordered, Status, DriverId, by, byLabel, nowUtc);
    }

    public DeliveryChange MarkPickedUp(Guid by, string? byLabel, DateTime nowUtc)
    {
        if (Status != DeliveryStatus.Assigned)
            throw new DeliveryDomainException("Dostawę można odebrać dopiero po przypisaniu.");
        Status = DeliveryStatus.InTransit;
        PickedUpAtUtc = nowUtc;
        return Changed(DeliveryChange.PickedUp, DeliveryStatus.Assigned, DriverId, by, byLabel, nowUtc);
    }

    public DeliveryChange MarkDelivered(Guid by, string? byLabel, DateTime nowUtc)
    {
        if (Status != DeliveryStatus.InTransit)
            throw new DeliveryDomainException("Dostawę można oznaczyć jako dostarczoną dopiero w trakcie dostawy.");
        Status = DeliveryStatus.Delivered;
        DeliveredAtUtc = nowUtc;
        return Changed(DeliveryChange.Delivered, DeliveryStatus.InTransit, DriverId, by, byLabel, nowUtc);
    }

    private DeliveryChange Changed(string action, DeliveryStatus from, Guid? previousDriver, Guid by, string? byLabel, DateTime nowUtc)
    {
        Version++;
        return new DeliveryChange(Id, action, from, Status, DriverId, previousDriver, StopSequence, Version, by, byLabel, nowUtc);
    }
}

/// <summary>Wpis historii dostawy (append-only, bez danych osobowych klienta): kto, kiedy, co zmienił.</summary>
public sealed class DeliveryChange : Entity
{
    public const string Assigned = "assigned";
    public const string Unassigned = "unassigned";
    public const string Reordered = "reordered";
    public const string PickedUp = "picked_up";
    public const string Delivered = "delivered";

    public Guid DeliveryId { get; private set; }
    public string Action { get; private set; } = default!;
    public DeliveryStatus FromStatus { get; private set; }
    public DeliveryStatus ToStatus { get; private set; }
    public Guid? DriverId { get; private set; }
    public Guid? PreviousDriverId { get; private set; }
    public int? StopSequence { get; private set; }
    public int Version { get; private set; }
    public Guid ActorId { get; private set; }
    public string? ActorLabel { get; private set; }
    public DateTime AtUtc { get; private set; }

    private DeliveryChange() { } // EF

    internal DeliveryChange(Guid deliveryId, string action, DeliveryStatus from, DeliveryStatus to, Guid? driverId,
        Guid? previousDriverId, int? stopSequence, int version, Guid actorId, string? actorLabel, DateTime atUtc)
    {
        DeliveryId = deliveryId;
        Action = action;
        FromStatus = from;
        ToStatus = to;
        DriverId = driverId;
        PreviousDriverId = previousDriverId;
        StopSequence = stopSequence;
        Version = version;
        ActorId = actorId;
        ActorLabel = actorLabel;
        AtUtc = atUtc;
    }
}
