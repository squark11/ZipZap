using ZipZap.BuildingBlocks.Domain;

namespace ZipZap.Modules.Ordering.Domain;

/// <summary>Stan kompletacji pozycji zamówienia w rundzie zakupowej.</summary>
public enum PickStatus { Pending, Bought, Unavailable, Substituted }

/// <summary>Żądana zmiana kompletacji (po znormalizowaniu porównywalna z bieżącym stanem).</summary>
public sealed record PickRequest(
    PickStatus Status, int PickedQuantity,
    Guid? SubstituteProductId = null, string? SubstituteProductName = null, string? SubstituteUnit = null,
    int? SubstituteQuantity = null, string? Note = null);

/// <summary>
/// Kompletacja JEDNEJ pozycji zamówienia (1:1 z <see cref="OrderItem"/>, Id = Id pozycji).
/// Oryginalna pozycja (nazwa, cena, ilość zamówiona) pozostaje nietknięta — tu jest tylko to, co operator
/// faktycznie kupił / czego nie było / czym zastąpił. Zamiana NIE zmienia cen ani kwot zamówienia (W1: bez opłat;
/// reguły cenowe zamienników nie są rozstrzygnięte). Każda zmiana podbija <see cref="Version"/> (optymistyczna
/// współbieżność) i zostawia wpis <see cref="OrderItemPickChange"/> (pełny ślad: kto, kiedy, co).
/// </summary>
public sealed class OrderItemPick : Entity
{
    public const int MaxQuantity = 1000;
    public const int MaxNoteLength = 300;

    public Guid OrderId { get; private set; }
    public Guid StoreId { get; private set; }
    public Guid PurchasingRoundId { get; private set; }
    public Guid ProductId { get; private set; }

    public PickStatus Status { get; private set; } = PickStatus.Pending;
    /// <summary>Faktycznie kupiona ilość ORYGINALNEGO produktu (0 dla niedostępnych i zamienionych).</summary>
    public int PickedQuantity { get; private set; }

    public Guid? SubstituteProductId { get; private set; }
    /// <summary>Migawka nazwy/jednostki zamiennika z chwili zapisu (katalog może się później zmienić).</summary>
    public string? SubstituteProductName { get; private set; }
    public string? SubstituteUnit { get; private set; }
    public int? SubstituteQuantity { get; private set; }
    public string? Note { get; private set; }

    /// <summary>0 = jeszcze nic nie zapisano (wiersza nie ma w bazie); każda zmiana +1.</summary>
    public int Version { get; private set; }
    public Guid? UpdatedBy { get; private set; }
    public string? UpdatedByLabel { get; private set; }
    public DateTime? UpdatedAtUtc { get; private set; }

    private OrderItemPick() { } // EF

    /// <summary>Stan początkowy (oczekuje) — niezapisany, dopóki operator czegoś nie zmieni.</summary>
    public static OrderItemPick Start(Guid orderItemId, Guid orderId, Guid storeId, Guid roundId, Guid productId) => new()
    {
        Id = orderItemId, OrderId = orderId, StoreId = storeId, PurchasingRoundId = roundId, ProductId = productId,
    };

    public PickRequest Current => new(Status, PickedQuantity, SubstituteProductId, SubstituteProductName,
        SubstituteUnit, SubstituteQuantity, Note);

    /// <summary>Czy żądanie (po normalizacji) opisuje dokładnie bieżący stan — np. podwójne kliknięcie.</summary>
    public bool Matches(PickRequest normalized) => Current == normalized;

    /// <summary>
    /// Waliduje i normalizuje żądanie: pola nieistotne dla statusu są czyszczone, notatka przycinana.
    /// Rzuca <see cref="OrderingDomainException"/> z komunikatem dla operatora.
    /// </summary>
    public PickRequest Normalize(PickRequest r)
    {
        var note = string.IsNullOrWhiteSpace(r.Note) ? null : r.Note.Trim();
        if (note is { Length: > MaxNoteLength })
            throw new OrderingDomainException($"Notatka może mieć najwyżej {MaxNoteLength} znaków.");

        switch (r.Status)
        {
            case PickStatus.Pending:
            case PickStatus.Unavailable:
                return new PickRequest(r.Status, 0, Note: note);

            case PickStatus.Bought:
                if (r.PickedQuantity is < 1 or > MaxQuantity)
                    throw new OrderingDomainException($"Podaj kupioną ilość (1–{MaxQuantity}).");
                return new PickRequest(PickStatus.Bought, r.PickedQuantity, Note: note);

            case PickStatus.Substituted:
                if (r.SubstituteProductId is not Guid sub || sub == Guid.Empty)
                    throw new OrderingDomainException("Wybierz produkt zastępczy.");
                if (sub == ProductId)
                    throw new OrderingDomainException("Produkt zastępczy musi być inny niż zamówiony.");
                if (string.IsNullOrWhiteSpace(r.SubstituteProductName))
                    throw new OrderingDomainException("Brak nazwy produktu zastępczego.");
                if (r.SubstituteQuantity is not int q || q < 1 || q > MaxQuantity)
                    throw new OrderingDomainException($"Podaj ilość produktu zastępczego (1–{MaxQuantity}).");
                return new PickRequest(PickStatus.Substituted, 0, sub, r.SubstituteProductName.Trim(),
                    string.IsNullOrWhiteSpace(r.SubstituteUnit) ? "szt" : r.SubstituteUnit.Trim(), q, note);

            default:
                throw new OrderingDomainException("Nieznany status kompletacji.");
        }
    }

    /// <summary>Zapisuje ZNORMALIZOWANE żądanie; zwraca wpis historii (append-only).</summary>
    public OrderItemPickChange Apply(PickRequest normalized, Guid by, string? byLabel, DateTime nowUtc)
    {
        var from = Status;
        Status = normalized.Status;
        PickedQuantity = normalized.PickedQuantity;
        SubstituteProductId = normalized.SubstituteProductId;
        SubstituteProductName = normalized.SubstituteProductName;
        SubstituteUnit = normalized.SubstituteUnit;
        SubstituteQuantity = normalized.SubstituteQuantity;
        Note = normalized.Note;
        Version++;
        UpdatedBy = by;
        UpdatedByLabel = byLabel;
        UpdatedAtUtc = nowUtc;
        return new OrderItemPickChange(this, from);
    }
}

/// <summary>Wpis historii kompletacji (append-only): pełny stan po zmianie + poprzedni status + autor i czas.</summary>
public sealed class OrderItemPickChange : Entity
{
    public Guid OrderItemId { get; private set; }
    public Guid OrderId { get; private set; }
    public Guid PurchasingRoundId { get; private set; }
    public int Version { get; private set; }
    public PickStatus FromStatus { get; private set; }
    public PickStatus Status { get; private set; }
    public int PickedQuantity { get; private set; }
    public Guid? SubstituteProductId { get; private set; }
    public string? SubstituteProductName { get; private set; }
    public string? SubstituteUnit { get; private set; }
    public int? SubstituteQuantity { get; private set; }
    public string? Note { get; private set; }
    public Guid ChangedBy { get; private set; }
    public string? ChangedByLabel { get; private set; }
    public DateTime ChangedAtUtc { get; private set; }

    private OrderItemPickChange() { } // EF

    internal OrderItemPickChange(OrderItemPick p, PickStatus from)
    {
        OrderItemId = p.Id;
        OrderId = p.OrderId;
        PurchasingRoundId = p.PurchasingRoundId;
        Version = p.Version;
        FromStatus = from;
        Status = p.Status;
        PickedQuantity = p.PickedQuantity;
        SubstituteProductId = p.SubstituteProductId;
        SubstituteProductName = p.SubstituteProductName;
        SubstituteUnit = p.SubstituteUnit;
        SubstituteQuantity = p.SubstituteQuantity;
        Note = p.Note;
        ChangedBy = p.UpdatedBy!.Value;
        ChangedByLabel = p.UpdatedByLabel;
        ChangedAtUtc = p.UpdatedAtUtc!.Value;
    }
}
