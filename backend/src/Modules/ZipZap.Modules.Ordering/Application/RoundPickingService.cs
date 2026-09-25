using Microsoft.EntityFrameworkCore;
using ZipZap.BuildingBlocks.Domain;
using ZipZap.BuildingBlocks.MultiTenancy;
using ZipZap.Modules.Ordering.Domain;
using ZipZap.Modules.Ordering.Infrastructure;

namespace ZipZap.Modules.Ordering.Application;

/// <summary>
/// Rundy zakupowe po stronie operatora: lista rund sklepu, lista zakupów (suma + rozbicie na zamówienia)
/// i kompletacja pozycji. Dostęp: admin albo pracownik TEGO sklepu (kierowca — brak dostępu).
/// Kompletacja NIE zmienia statusu zamówienia, kwot ani cen i nie publikuje zdarzeń (w tym OrderDelivered).
/// Zamówienia są przypisane do rundy przy checkoucie — tu nigdy nie zmieniamy tego przypisania.
/// </summary>
public sealed class RoundPickingService
{
    private readonly OrderingDbContext _db;
    private readonly ICurrentUser _user;
    private readonly PurchasingRoundService _rounds;

    public RoundPickingService(OrderingDbContext db, ICurrentUser user, PurchasingRoundService rounds)
    {
        _db = db;
        _user = user;
        _rounds = rounds;
    }

    private Result Guard(Guid storeId) => _user.ManagesStore(storeId)
        ? Result.Success()
        : Result.Failure(Error.Forbidden("Brak uprawnień do rund zakupowych tego sklepu."));

    /// <summary>
    /// Zamówienia liczone do zakupów: testowe (W1) od złożenia, płatne dopiero po potwierdzeniu płatności.
    /// Anulowane i nieopłacone są widoczne, ale poza listą zakupów.
    /// </summary>
    private static (bool Included, string? Reason) Inclusion(Order o) => o.Status switch
    {
        OrderStatus.Cancelled => (false, "Anulowane"),
        OrderStatus.Placed when !o.IsTestOrder => (false, "Czeka na płatność"),
        _ => (true, null),
    };

    /// <summary>Poprawki kompletacji możliwe do przekazania kierowcy (potem stan jest zamrożony).</summary>
    private static bool Editable(Order o) => Inclusion(o).Included
        && o.Status is OrderStatus.Placed or OrderStatus.Confirmed or OrderStatus.Picking or OrderStatus.ReadyForPickup;

    private static (string State, string Label) State(PurchasingRound r, DateTime now, int included, RoundProgressDto p)
    {
        if (now < r.CutoffAtUtc) return ("accepting", "Przyjmuje zamówienia");
        if (included == 0) return ("empty", "Brak zamówień");
        if (p.Pending > 0)
            return now < r.StartsAtUtc ? ("to_shop", "Zamknięta — czeka na zakupy") : ("shopping", "Zakupy w toku");
        return ("picked", "Skompletowana");
    }

    // ---------------- Lista rund ----------------

    public async Task<Result<IReadOnlyList<RoundSummaryDto>>> ListRoundsAsync(Guid storeId, int pastDays, CancellationToken ct)
    {
        var guard = Guard(storeId);
        if (guard.IsFailure) return guard.Error;

        var now = _rounds.UtcNow;
        var since = now.AddDays(-Math.Clamp(pastDays, 0, 30));
        var rounds = await _db.PurchasingRounds.AsNoTracking()
            .Where(r => r.StoreId == storeId && r.StartsAtUtc >= since)
            .OrderBy(r => r.StartsAtUtc).ToListAsync(ct);

        var roundIds = rounds.Select(r => r.Id).ToList();
        var orders = await _db.Orders.AsNoTracking().Include(o => o.Items)
            .Where(o => o.StoreId == storeId && o.PurchasingRoundId != null && roundIds.Contains(o.PurchasingRoundId.Value))
            .ToListAsync(ct);
        var picks = await PicksAsync(roundIds, ct);

        var result = rounds.Select(r =>
        {
            var roundOrders = orders.Where(o => o.PurchasingRoundId == r.Id).ToList();
            return Summary(r, roundOrders, Lines(roundOrders.Where(o => Inclusion(o).Included), picks), now);
        }).ToList();

        // Najbliższa runda z harmonogramu bez zamówień (jeszcze nieutrwalona) — czytelny stan „0 zamówień".
        var next = await _rounds.ResolveAsync(ct);
        if (next is not null && rounds.All(r => r.StartsAtUtc != next.Occurrence.StartsAtUtc))
        {
            var o = next.Occurrence;
            var info = PurchasingRoundInfo.From(o.LocalDate, o.LocalTime, o.StartsAtUtc, o.CutoffAtUtc, next.TimeZone, next.TimeZoneId);
            result.Add(new RoundSummaryDto(null, info, "accepting", "Przyjmuje zamówienia", 0, 0, new RoundProgressDto(0, 0, 0, 0, 0)));
        }

        return result.OrderBy(s => s.Round.StartsAtUtc).ToList();
    }

    // ---------------- Szczegóły rundy ----------------

    public async Task<Result<RoundDetailDto>> GetRoundAsync(Guid storeId, Guid roundId, CancellationToken ct)
    {
        var guard = Guard(storeId);
        if (guard.IsFailure) return guard.Error;

        var round = await _db.PurchasingRounds.AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == roundId && r.StoreId == storeId, ct);
        if (round is null) return Error.NotFound("Runda nie istnieje w tym sklepie.");

        var orders = await _db.Orders.AsNoTracking().Include(o => o.Items)
            .Where(o => o.StoreId == storeId && o.PurchasingRoundId == roundId)
            .OrderBy(o => o.PlacedAtUtc).ToListAsync(ct);
        var picks = await PicksAsync(new List<Guid> { roundId }, ct);
        var slotIds = orders.Select(o => o.TimeSlotId).Distinct().ToList();
        var slots = await _db.TimeSlots.AsNoTracking().Where(s => slotIds.Contains(s.Id)).ToDictionaryAsync(s => s.Id, ct);

        var includedLines = Lines(orders.Where(o => Inclusion(o).Included), picks);
        var orderDtos = orders.Select(o =>
        {
            var (included, reason) = Inclusion(o);
            slots.TryGetValue(o.TimeSlotId, out var slot);
            return new RoundOrderDto(o.Id, ShoppingListBuilder.OrderCode(o.Id), ShoppingListBuilder.CustomerCode(o.CustomerId),
                o.Status.ToString(), o.IsTestOrder, included, reason, Editable(o),
                slot?.Date, slot?.StartTime, slot?.EndTime, Lines(new[] { o }, picks));
        }).ToList();

        return new RoundDetailDto(Summary(round, orders, includedLines, _rounds.UtcNow),
            ShoppingListBuilder.Build(includedLines), orderDtos);
    }

    // ---------------- Kompletacja ----------------

    public async Task<Result<PickDto>> UpdatePickAsync(Guid storeId, Guid roundId, Guid orderItemId,
        PickUpdateInput input, CancellationToken ct)
    {
        var guard = Guard(storeId);
        if (guard.IsFailure) return guard.Error;
        if (_user.UserId is not Guid by) return Error.Unauthorized("Wymagane logowanie.");
        if (!Enum.TryParse<PickStatus>(input.Status, ignoreCase: true, out var status) || !Enum.IsDefined(status)
            || int.TryParse(input.Status, out _))
            return Error.Validation("Nieznany status kompletacji (Pending, Bought, Unavailable, Substituted).");

        var order = await FindOrderOfItemAsync(storeId, roundId, orderItemId, ct);
        if (order is null) return Error.NotFound("Pozycja nie należy do tej rundy.");
        if (!Editable(order))
            return Error.Conflict($"Zamówienia {ShoppingListBuilder.OrderCode(order.Id)} nie można już kompletować " +
                $"(status: {order.Status}).");
        var item = order.Items.Single(i => i.Id == orderItemId);

        string? subName = null, subUnit = null;
        if (status == PickStatus.Substituted && input.SubstituteProductId is Guid subId)
        {
            var product = await _db.CatalogProducts.AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == subId && p.StoreId == storeId, ct);
            if (product is null) return Error.Validation("Produkt zastępczy nie należy do katalogu tego sklepu.");
            subName = product.Name;
            subUnit = product.Unit;
        }

        var pick = await _db.OrderItemPicks.FirstOrDefaultAsync(p => p.Id == orderItemId, ct);
        var isNew = pick is null;
        pick ??= OrderItemPick.Start(item.Id, order.Id, storeId, roundId, item.ProductId);

        PickRequest normalized;
        try
        {
            normalized = pick.Normalize(new PickRequest(status, input.PickedQuantity ?? 0, input.SubstituteProductId,
                subName, subUnit, input.SubstituteQuantity, input.Note));
        }
        catch (OrderingDomainException ex) { return Error.Validation(ex.Message); }

        // Ten sam stan co zapisany (podwójne kliknięcie / ponowienie) — bez nowego wpisu historii.
        if (pick.Matches(normalized)) return PickDto.From(pick);
        // Operator widział starszą wersję — nie nadpisujemy nowszej zmiany.
        if (input.ExpectedVersion != pick.Version) return Stale(pick);

        var change = pick.Apply(normalized, by, _user.Email, _rounds.UtcNow);
        if (isNew) _db.OrderItemPicks.Add(pick);
        _db.OrderItemPickChanges.Add(change);
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex is DbUpdateConcurrencyException
            || ex.InnerException is Npgsql.PostgresException { SqlState: "23505" })
        {
            // Równoległy zapis tej samej pozycji wygrał: jeśli zapisał to samo — OK, inaczej konflikt.
            _db.ChangeTracker.Clear();
            var fresh = await _db.OrderItemPicks.AsNoTracking().FirstOrDefaultAsync(p => p.Id == orderItemId, ct);
            if (fresh is not null && fresh.Matches(normalized)) return PickDto.From(fresh);
            return Stale(fresh);
        }
        return PickDto.From(pick);
    }

    public async Task<Result<IReadOnlyList<PickChangeDto>>> GetPickHistoryAsync(Guid storeId, Guid roundId,
        Guid orderItemId, CancellationToken ct)
    {
        var guard = Guard(storeId);
        if (guard.IsFailure) return guard.Error;
        if (await FindOrderOfItemAsync(storeId, roundId, orderItemId, ct) is null)
            return Error.NotFound("Pozycja nie należy do tej rundy.");

        var changes = await _db.OrderItemPickChanges.AsNoTracking()
            .Where(h => h.OrderItemId == orderItemId).OrderBy(h => h.Version).ToListAsync(ct);
        return changes.Select(PickChangeDto.From).ToList();
    }

    // ---------------- Pomocnicze ----------------

    private static Error Stale(OrderItemPick? current) => Error.Conflict(
        "Pozycja została w międzyczasie zmieniona" +
        (current?.UpdatedByLabel is { } who ? $" (przez {who})" : "") +
        " — odśwież rundę i sprawdź stan przed ponownym zapisem.");

    private Task<Order?> FindOrderOfItemAsync(Guid storeId, Guid roundId, Guid orderItemId, CancellationToken ct)
        => _db.Orders.AsNoTracking().Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.StoreId == storeId && o.PurchasingRoundId == roundId
                && o.Items.Any(i => i.Id == orderItemId), ct);

    private async Task<Dictionary<Guid, OrderItemPick>> PicksAsync(List<Guid> roundIds, CancellationToken ct)
        => await _db.OrderItemPicks.AsNoTracking()
            .Where(p => roundIds.Contains(p.PurchasingRoundId)).ToDictionaryAsync(p => p.Id, ct);

    private static List<PickLineDto> Lines(IEnumerable<Order> orders, IReadOnlyDictionary<Guid, OrderItemPick> picks) =>
        orders.SelectMany(o => o.Items.Select(i => new PickLineDto(
                o.Id, ShoppingListBuilder.OrderCode(o.Id), ShoppingListBuilder.CustomerCode(o.CustomerId), i.Id,
                i.ProductId, i.ProductName, i.Unit, i.Quantity, Editable(o),
                picks.TryGetValue(i.Id, out var p) ? PickDto.From(p) : PickDto.Pending(i.Id))))
            .ToList();

    private static RoundSummaryDto Summary(PurchasingRound r, IReadOnlyCollection<Order> orders,
        IReadOnlyCollection<PickLineDto> includedLines, DateTime now)
    {
        var included = orders.Count(o => Inclusion(o).Included);
        var progress = ShoppingListBuilder.Progress(includedLines);
        var (state, label) = State(r, now, included, progress);
        return new RoundSummaryDto(r.Id, PurchasingRoundService.ToInfo(r), state, label,
            included, orders.Count - included, progress);
    }
}
