using ZipZap.Modules.Ordering.Domain;

namespace ZipZap.Modules.Ordering.Application;

/// <summary>Stan kompletacji pozycji (Version 0 = jeszcze nic nie zapisano).</summary>
public sealed record PickDto(
    Guid OrderItemId, string Status, int PickedQuantity,
    Guid? SubstituteProductId, string? SubstituteProductName, string? SubstituteUnit, int? SubstituteQuantity,
    string? Note, int Version, string? UpdatedBy, DateTime? UpdatedAtUtc)
{
    public static PickDto From(OrderItemPick p) => new(p.Id, p.Status.ToString(), p.PickedQuantity,
        p.SubstituteProductId, p.SubstituteProductName, p.SubstituteUnit, p.SubstituteQuantity,
        p.Note, p.Version, p.UpdatedByLabel, p.UpdatedAtUtc);

    public static PickDto Pending(Guid orderItemId) =>
        new(orderItemId, nameof(PickStatus.Pending), 0, null, null, null, null, null, 0, null, null);
}

/// <summary>
/// Pozycja zamówienia w rundzie z przypisaniem do zamówienia i klienta. Klient jest opisany TYLKO kodem
/// (bez imienia, telefonu i adresu) — do kompletacji wystarczy oznaczyć torbę kodem zamówienia.
/// </summary>
public sealed record PickLineDto(
    Guid OrderId, string OrderCode, string CustomerCode, Guid OrderItemId,
    Guid ProductId, string ProductName, string Unit, int Quantity, bool Editable, PickDto Pick);

/// <summary>Wiersz listy zakupów: suma dla rundy + rozbicie na zamówienia (agregacja niczego nie zaciera).</summary>
public sealed record ShoppingListRowDto(
    Guid ProductId, string ProductName, string Unit,
    int OrderedQuantity, int BoughtQuantity,
    int PendingLines, int BoughtLines, int UnavailableLines, int SubstitutedLines,
    IReadOnlyList<PickLineDto> Lines);

public sealed record RoundProgressDto(int Lines, int Pending, int Bought, int Unavailable, int Substituted);

public sealed record RoundOrderDto(
    Guid OrderId, string OrderCode, string CustomerCode, string Status, bool IsTestOrder,
    bool Included, string? ExcludedReason, bool Editable,
    DateOnly? DeliveryDate, TimeOnly? DeliveryStartTime, TimeOnly? DeliveryEndTime,
    IReadOnlyList<PickLineDto> Items);

/// <summary>Runda na liście. <c>Id = null</c> — najbliższa runda z harmonogramu, która nie ma jeszcze zamówień.</summary>
public sealed record RoundSummaryDto(
    Guid? Id, PurchasingRoundInfo Round, string State, string StateLabel,
    int OrderCount, int ExcludedOrderCount, RoundProgressDto Progress);

public sealed record RoundDetailDto(
    RoundSummaryDto Summary, IReadOnlyList<ShoppingListRowDto> ShoppingList, IReadOnlyList<RoundOrderDto> Orders);

public sealed record PickChangeDto(
    int Version, string FromStatus, string Status, int PickedQuantity,
    string? SubstituteProductName, string? SubstituteUnit, int? SubstituteQuantity,
    string? Note, string? ChangedBy, DateTime ChangedAtUtc)
{
    public static PickChangeDto From(OrderItemPickChange h) => new(h.Version, h.FromStatus.ToString(), h.Status.ToString(),
        h.PickedQuantity, h.SubstituteProductName, h.SubstituteUnit, h.SubstituteQuantity, h.Note, h.ChangedByLabel, h.ChangedAtUtc);
}

/// <summary>Zmiana kompletacji od operatora. <c>ExpectedVersion</c> = wersja, którą operator widział.</summary>
public sealed record PickUpdateInput(
    string Status, int? PickedQuantity, Guid? SubstituteProductId, int? SubstituteQuantity, string? Note, int ExpectedVersion);

/// <summary>Czysta logika listy zakupów i postępu (bez bazy) — testowana jednostkowo.</summary>
public static class ShoppingListBuilder
{
    private static readonly StringComparer PolishOrder =
        StringComparer.Create(new System.Globalization.CultureInfo("pl-PL"), ignoreCase: true);

    /// <summary>Sumuje ten sam produkt (i jednostkę) z wielu zamówień; każda pozycja zostaje w <c>Lines</c>.</summary>
    public static IReadOnlyList<ShoppingListRowDto> Build(IEnumerable<PickLineDto> lines) =>
        lines.GroupBy(l => (l.ProductId, l.Unit))
            .Select(g =>
            {
                var items = g.OrderBy(l => l.OrderCode, StringComparer.Ordinal).ThenBy(l => l.OrderItemId).ToList();
                return new ShoppingListRowDto(
                    g.Key.ProductId, items[0].ProductName, g.Key.Unit,
                    items.Sum(l => l.Quantity),
                    items.Where(l => Is(l, PickStatus.Bought)).Sum(l => l.Pick.PickedQuantity),
                    items.Count(l => Is(l, PickStatus.Pending)),
                    items.Count(l => Is(l, PickStatus.Bought)),
                    items.Count(l => Is(l, PickStatus.Unavailable)),
                    items.Count(l => Is(l, PickStatus.Substituted)),
                    items);
            })
            .OrderBy(r => r.ProductName, PolishOrder).ThenBy(r => r.Unit, StringComparer.Ordinal)
            .ToList();

    public static RoundProgressDto Progress(IReadOnlyCollection<PickLineDto> lines) => new(
        lines.Count,
        lines.Count(l => Is(l, PickStatus.Pending)),
        lines.Count(l => Is(l, PickStatus.Bought)),
        lines.Count(l => Is(l, PickStatus.Unavailable)),
        lines.Count(l => Is(l, PickStatus.Substituted)));

    private static bool Is(PickLineDto l, PickStatus s) => l.Pick.Status == s.ToString();

    /// <summary>Kod zamówienia jak w panelu zamówień (8 pierwszych znaków identyfikatora).</summary>
    public static string OrderCode(Guid orderId) => orderId.ToString("N")[..8];

    /// <summary>Pseudonimowy kod klienta — pozwala rozróżnić klientów bez danych osobowych.</summary>
    public static string CustomerCode(Guid customerId) => "K-" + customerId.ToString("N")[..6].ToUpperInvariant();
}
