using Microsoft.EntityFrameworkCore;
using ZipZap.BuildingBlocks.Domain;
using ZipZap.BuildingBlocks.Messaging;
using ZipZap.BuildingBlocks.MultiTenancy;
using ZipZap.BuildingBlocks.Outbox;
using ZipZap.Contracts.Ordering;
using ZipZap.Modules.Delivery.Domain;
using ZipZap.Modules.Delivery.Infrastructure;

namespace ZipZap.Modules.Delivery.Application;

public sealed record DeliveryDto(
    Guid Id, Guid OrderId, Guid StoreId, Guid? DriverId, string Status,
    DateTime CreatedAtUtc, DateTime? PickedUpAtUtc, DateTime? DeliveredAtUtc,
    DateOnly? DeliveryDate = null, TimeOnly? WindowStart = null, TimeOnly? WindowEnd = null,
    int? StopSequence = null, int Version = 0)
{
    public static DeliveryDto From(Domain.Delivery d) =>
        new(d.Id, d.OrderId, d.StoreId, d.DriverId, d.Status.ToString(), d.CreatedAtUtc, d.PickedUpAtUtc, d.DeliveredAtUtc,
            d.DeliveryDate, d.WindowStart, d.WindowEnd, d.StopSequence, d.Version);
}

/// <summary>Dostawa w widoku operatora sklepu — z adresem i telefonem (operator ma prawo do danych SWOJEGO sklepu).</summary>
public sealed record BoardDeliveryDto(
    Guid Id, Guid OrderId, string OrderCode, string CustomerCode, string Status,
    Guid? DriverId, string? DriverName, int? StopSequence, int Version,
    DateOnly? DeliveryDate, TimeOnly? WindowStart, TimeOnly? WindowEnd,
    string? Address, string? Phone, int ItemCount, bool IsTestOrder,
    DateTime? AssignedAtUtc, DateTime? PickedUpAtUtc, DateTime? DeliveredAtUtc);

public sealed record DeliveryBoardDto(IReadOnlyList<BoardDeliveryDto> Deliveries, IReadOnlyList<DriverSummary> Drivers);

/// <summary>
/// Dostawa w widoku kierowcy. Adres i telefon są wypełnione WYŁĄCZNIE dla dostaw przypisanych temu kierowcy
/// i w realizacji (przypisana / w drodze); po dostarczeniu znikają. Tylko dane potrzebne do dostawy.
/// </summary>
public sealed record DriverDeliveryDto(
    Guid Id, string OrderCode, string Status, int? StopSequence, int Version,
    DateOnly? DeliveryDate, TimeOnly? WindowStart, TimeOnly? WindowEnd,
    string? Address, string? Phone, int ItemCount, bool IsTestOrder,
    DateTime? PickedUpAtUtc, DateTime? DeliveredAtUtc);

public sealed record DeliveryChangeDto(
    string Action, string FromStatus, string ToStatus, Guid? DriverId, Guid? PreviousDriverId,
    int? StopSequence, int Version, string? Actor, DateTime AtUtc, string? Reason = null)
{
    public static DeliveryChangeDto From(DeliveryChange c) => new(c.Action, c.FromStatus.ToString(), c.ToStatus.ToString(),
        c.DriverId, c.PreviousDriverId, c.StopSequence, c.Version, c.ActorLabel, c.AtUtc, c.Reason);
}

public sealed record AssignDriverInput(Guid DriverId, int ExpectedVersion);
/// <summary>Awaryjna akcja administratora: <c>picked_up</c> albo <c>delivered</c>, z powodem (bez danych klienta).</summary>
public sealed record AdminOverrideInput(string Action, string Reason, int ExpectedVersion);
public sealed record UnassignDriverInput(int ExpectedVersion);
public sealed record RouteStopInput(Guid DeliveryId, int ExpectedVersion);
public sealed record RouteOrderInput(
    Guid DriverId, DateOnly? DeliveryDate, TimeOnly? WindowStart, TimeOnly? WindowEnd, IReadOnlyList<RouteStopInput> Stops);

/// <summary>
/// Dostawy: przydział i kolejność (operator sklepu), odbiór i dostarczenie (przypisany kierowca).
/// Dane klienta: operator — tylko dostawy swojego sklepu; kierowca — tylko SWOJE dostawy w realizacji, po
/// sprawdzeniu w bazie (nie w tokenie), że konto jest aktywne i że jest kierowcą sklepu tej dostawy.
/// </summary>
public sealed class DeliveryService
{
    private readonly DeliveryDbContext _db;
    private readonly ICurrentUser _user;
    private readonly IIntegrationEventTypeRegistry _events;
    private readonly IDriverDirectory _drivers;
    private readonly IOrderContactProvider _contacts;

    public DeliveryService(DeliveryDbContext db, ICurrentUser user, IIntegrationEventTypeRegistry events,
        IDriverDirectory drivers, IOrderContactProvider contacts)
    {
        _db = db;
        _user = user;
        _events = events;
        _drivers = drivers;
        _contacts = contacts;
    }

    private bool IsAdmin => _user.Roles.Contains("Admin");

    // ================= Kierowca =================

    /// <summary>
    /// Pula dostępnych dostaw — WYŁĄCZNIE ze sklepów, do których kierowca jest przypisany
    /// (claim <c>driver_store_id</c>). Admin widzi wszystkie. Bez danych klienta.
    /// </summary>
    public async Task<IReadOnlyList<DeliveryDto>> ListAvailableAsync(CancellationToken ct)
    {
        var query = _db.Deliveries.AsNoTracking().Where(d => d.Status == DeliveryStatus.AvailableForPickup);
        if (!IsAdmin)
        {
            var stores = _user.DriverStoreIds.ToArray();
            if (stores.Length == 0) return Array.Empty<DeliveryDto>();
            query = query.Where(d => stores.Contains(d.StoreId));
        }
        return await query.OrderBy(d => d.CreatedAtUtc).Select(d => DeliveryDto.From(d)).ToListAsync(ct);
    }

    /// <summary>Moje dostawy: w realizacji (z adresem/telefonem) + dostarczone w ostatniej dobie (bez danych klienta).</summary>
    public async Task<IReadOnlyList<DriverDeliveryDto>> ListMineAsync(CancellationToken ct)
    {
        if (_user.UserId is not Guid me) return Array.Empty<DriverDeliveryDto>();
        var since = DateTime.UtcNow.AddHours(-24);
        var mine = await _db.Deliveries.AsNoTracking()
            .Where(d => d.DriverId == me && (d.Status == DeliveryStatus.Assigned || d.Status == DeliveryStatus.InTransit
                || (d.Status == DeliveryStatus.Delivered && d.DeliveredAtUtc >= since)))
            .ToListAsync(ct);

        // Aktualne uprawnienia z bazy: konto aktywne + przypisanie do sklepu dostawy.
        var allowedStores = new HashSet<Guid>();
        foreach (var storeId in mine.Select(d => d.StoreId).Distinct())
            if (await _drivers.IsActiveDriverOfStoreAsync(me, storeId, ct)) allowedStores.Add(storeId);
        var visible = mine.Where(d => allowedStores.Contains(d.StoreId)).ToList();

        var infos = await _contacts.GetAsync(visible.Select(d => d.OrderId).ToList(), ct);

        return visible
            .OrderBy(d => d.DeliveryDate).ThenBy(d => d.WindowStart).ThenBy(d => d.StopSequence ?? int.MaxValue)
            .Select(d =>
            {
                var info = infos.GetValueOrDefault(d.OrderId);
                var showContact = InProgress(d) && info is not null; // po dostarczeniu adres/telefon znikają
                return new DriverDeliveryDto(d.Id, info?.OrderCode ?? d.OrderId.ToString("N")[..8], d.Status.ToString(),
                    d.StopSequence, d.Version, d.DeliveryDate, d.WindowStart, d.WindowEnd,
                    showContact ? info!.Address : null, showContact ? info!.Phone : null,
                    info?.ItemCount ?? 0, info?.IsTestOrder ?? false, d.PickedUpAtUtc, d.DeliveredAtUtc);
            })
            .ToList();
    }

    private static bool InProgress(Domain.Delivery d) => d.Status is DeliveryStatus.Assigned or DeliveryStatus.InTransit;

    /// <summary>Samodzielne przyjmowanie z puli jest wyłączone: dostawy przydziela operator sklepu (S1c).</summary>
    public Task<Result<DeliveryDto>> AcceptAsync(Guid deliveryId, CancellationToken ct)
        => Task.FromResult(Result.Failure<DeliveryDto>(Error.Forbidden(
            "Dostawy przydziela operator sklepu — poproś o przypisanie w panelu sklepu.")));

    public Task<Result<DeliveryDto>> PickUpAsync(Guid deliveryId, CancellationToken ct)
        => DriverStepAsync(deliveryId, (d, by, label, now) => d.MarkPickedUp(by, label, now),
            d => new OrderPickedUp(d.OrderId, d.StoreId), ct);

    public Task<Result<DeliveryDto>> DeliverAsync(Guid deliveryId, CancellationToken ct)
        => DriverStepAsync(deliveryId, (d, by, label, now) => d.MarkDelivered(by, label, now),
            d => new OrderDelivered(d.OrderId, d.StoreId), ct);

    private async Task<Result<DeliveryDto>> DriverStepAsync(Guid deliveryId,
        Func<Domain.Delivery, Guid, string?, DateTime, DeliveryChange> step, Func<Domain.Delivery, IIntegrationEvent> emit,
        CancellationToken ct)
    {
        if (_user.UserId is not Guid me)
            return Result.Failure<DeliveryDto>(Error.Unauthorized("Wymagane logowanie kierowcy."));

        var delivery = await _db.Deliveries.FirstOrDefaultAsync(d => d.Id == deliveryId, ct);
        if (delivery is null) return Result.Failure<DeliveryDto>(Error.NotFound("Dostawa nie istnieje."));

        // Sprawdzenia PRZED jakąkolwiek zmianą stanu i przed emisją OrderPickedUp/OrderDelivered — BEZ WYJĄTKU dla
        // administratora: tylko kierowca, któremu przypisano TĘ dostawę, z aktywnym (w bazie) przypisaniem do jej sklepu.
        // Awaryjna zmiana przez administratora to osobna, audytowana akcja z powodem (OverrideAsync).
        if (!delivery.IsOwnedBy(me))
            return Result.Failure<DeliveryDto>(Error.Forbidden("To nie jest Twoja dostawa."));
        if (!await _drivers.IsActiveDriverOfStoreAsync(me, delivery.StoreId, ct))
            return Result.Failure<DeliveryDto>(Error.Forbidden("Nie jesteś aktywnym kierowcą sklepu tej dostawy."));

        DeliveryChange change;
        try { change = step(delivery, me, _user.Email, DateTime.UtcNow); }
        catch (DeliveryDomainException ex) { return Result.Failure<DeliveryDto>(Error.Conflict(ex.Message)); }

        return await CommitStepAsync(delivery, change, emit, ct);
    }

    /// <summary>
    /// Awaryjne potwierdzenie odbioru/dostarczenia przez ADMINISTRATORA (np. telefon kierowcy nie działa):
    /// wymagany powód i oczekiwana wersja; te same reguły przejść (bez przeskakiwania przypisania); osobny wpis
    /// historii „override_*" z powodem + audyt. Zdarzenia jak przy zwykłym przejściu — dokładnie raz.
    /// </summary>
    public async Task<Result<DeliveryDto>> OverrideAsync(Guid deliveryId, AdminOverrideInput input, CancellationToken ct)
    {
        if (!IsAdmin) return Result.Failure<DeliveryDto>(Error.Forbidden("Awaryjna zmiana jest dostępna tylko dla administratora."));
        if (_user.UserId is not Guid by) return Result.Failure<DeliveryDto>(Error.Unauthorized("Wymagane logowanie."));
        var action = (input.Action ?? "").Trim().ToLowerInvariant();
        if (action is not ("picked_up" or "delivered"))
            return Result.Failure<DeliveryDto>(Error.Validation("Akcja awaryjna: picked_up albo delivered."));

        var reason = (input.Reason ?? "").Trim();
        if (reason.Length is < 10 or > DeliveryChange.MaxReasonLength)
            return Result.Failure<DeliveryDto>(Error.Validation(
                $"Podaj powód awaryjnej zmiany (10–{DeliveryChange.MaxReasonLength} znaków, bez danych klienta)."));

        var delivery = await _db.Deliveries.FirstOrDefaultAsync(d => d.Id == deliveryId, ct);
        if (delivery is null) return Result.Failure<DeliveryDto>(Error.NotFound("Dostawa nie istnieje."));
        if (delivery.Version != input.ExpectedVersion) return Stale<DeliveryDto>();

        DeliveryChange change;
        try
        {
            var now = DateTime.UtcNow;
            change = action == "picked_up"
                ? delivery.OverridePickedUp(by, _user.Email, reason, now)
                : delivery.OverrideDelivered(by, _user.Email, reason, now);
        }
        catch (DeliveryDomainException ex) { return Result.Failure<DeliveryDto>(Error.Conflict(ex.Message)); }

        return await CommitStepAsync(delivery, change,
            action == "picked_up" ? d => new OrderPickedUp(d.OrderId, d.StoreId) : d => new OrderDelivered(d.OrderId, d.StoreId), ct);
    }

    private async Task<Result<DeliveryDto>> CommitStepAsync(Domain.Delivery delivery, DeliveryChange change,
        Func<Domain.Delivery, IIntegrationEvent> emit, CancellationToken ct)
    {
        _db.DeliveryChanges.Add(change);
        _db.AddOutboxMessage(emit(delivery), _events); // zdarzenie tylko po poprawnym przejściu, w tej samej transakcji

        try { await _db.SaveChangesAsync(ct); }
        catch (DbUpdateException ex) when (IsConcurrentChange(ex))
        {
            return Result.Failure<DeliveryDto>(Error.Conflict("Dostawa została właśnie zmieniona — odśwież widok."));
        }
        return DeliveryDto.From(delivery);
    }

    /// <summary>
    /// Równoległa zmiana tej samej dostawy: token xmin (0 zmienionych wierszy) albo unikalny wpis historii
    /// (dostawa + wersja) — w obu przypadkach wygrał ktoś inny, a ten zapis nie wprowadził żadnej zmiany.
    /// </summary>
    private static bool IsConcurrentChange(DbUpdateException ex)
        => ex is DbUpdateConcurrencyException || ex.InnerException is Npgsql.PostgresException { SqlState: "23505" };

    // ================= Operator sklepu =================

    /// <summary>Wszystkie dostawy sklepu (bez danych klienta) — zgodność wsteczna.</summary>
    public async Task<Result<IReadOnlyList<DeliveryDto>>> ListForStoreAsync(Guid storeId, CancellationToken ct)
    {
        if (!_user.ManagesStore(storeId))
            return Result.Failure<IReadOnlyList<DeliveryDto>>(Error.Forbidden("Brak dostępu do dostaw tego sklepu."));

        var items = await _db.Deliveries.AsNoTracking()
            .Where(d => d.StoreId == storeId)
            .OrderByDescending(d => d.CreatedAtUtc)
            .Select(d => DeliveryDto.From(d))
            .ToListAsync(ct);
        return Result.Success<IReadOnlyList<DeliveryDto>>(items);
    }

    /// <summary>Tablica dostaw sklepu: filtr dnia (okna dostawy) i statusu + aktywni kierowcy sklepu.</summary>
    public async Task<Result<DeliveryBoardDto>> GetBoardAsync(Guid storeId, DateOnly? date, string? status, CancellationToken ct)
    {
        if (!_user.ManagesStore(storeId))
            return Result.Failure<DeliveryBoardDto>(Error.Forbidden("Brak dostępu do dostaw tego sklepu."));

        var query = _db.Deliveries.AsNoTracking().Where(d => d.StoreId == storeId);
        if (date is DateOnly day) query = query.Where(d => d.DeliveryDate == day);
        if (!string.IsNullOrWhiteSpace(status))
        {
            if (!Enum.TryParse<DeliveryStatus>(status, ignoreCase: true, out var st) || !Enum.IsDefined(st) || int.TryParse(status, out _))
                return Result.Failure<DeliveryBoardDto>(Error.Validation("Nieznany status dostawy."));
            query = query.Where(d => d.Status == st);
        }

        var list = await query.OrderBy(d => d.DeliveryDate).ThenBy(d => d.WindowStart).ThenBy(d => d.StopSequence)
            .ThenBy(d => d.CreatedAtUtc).ToListAsync(ct);
        var contacts = await _contacts.GetAsync(list.Select(d => d.OrderId).ToList(), ct);
        var drivers = await _drivers.ListActiveDriversAsync(storeId, ct);
        var names = drivers.ToDictionary(x => x.Id, x => x.FullName);

        var rows = list.Select(d =>
        {
            contacts.TryGetValue(d.OrderId, out var c);
            string? driverName = d.DriverId is Guid drv
                ? names.GetValueOrDefault(drv) ?? "kierowca nieaktywny lub odpięty od sklepu"
                : null;
            return new BoardDeliveryDto(d.Id, d.OrderId, c?.OrderCode ?? d.OrderId.ToString("N")[..8], c?.CustomerCode ?? "—",
                d.Status.ToString(), d.DriverId, driverName, d.StopSequence, d.Version,
                d.DeliveryDate, d.WindowStart, d.WindowEnd, c?.Address, c?.Phone, c?.ItemCount ?? 0, c?.IsTestOrder ?? false,
                d.AssignedAtUtc, d.PickedUpAtUtc, d.DeliveredAtUtc);
        }).ToList();
        return new DeliveryBoardDto(rows, drivers);
    }

    /// <summary>Operator przypisuje (lub przepina) dostawę aktywnemu kierowcy SWOJEGO sklepu — na koniec trasy.</summary>
    public async Task<Result<DeliveryDto>> AssignAsync(Guid storeId, Guid deliveryId, AssignDriverInput input, CancellationToken ct)
    {
        if (!_user.ManagesStore(storeId)) return Result.Failure<DeliveryDto>(Error.Forbidden("Brak dostępu do dostaw tego sklepu."));
        if (_user.UserId is not Guid by) return Result.Failure<DeliveryDto>(Error.Unauthorized("Wymagane logowanie."));

        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        var delivery = await _db.Deliveries.FirstOrDefaultAsync(d => d.Id == deliveryId && d.StoreId == storeId, ct);
        if (delivery is null) return Result.Failure<DeliveryDto>(Error.NotFound("Dostawa nie istnieje w tym sklepie."));

        // Podwójne kliknięcie tego samego przypisania — bez zmian i bez wpisu historii.
        if (delivery.Status == DeliveryStatus.Assigned && delivery.DriverId == input.DriverId) return DeliveryDto.From(delivery);
        if (delivery.Version != input.ExpectedVersion) return Stale<DeliveryDto>();
        if (!await _drivers.IsActiveDriverOfStoreAsync(input.DriverId, storeId, ct))
            return Result.Failure<DeliveryDto>(Error.Validation("Wybrana osoba nie jest aktywnym kierowcą tego sklepu."));

        // Blokada trasy docelowej (kierowca + dzień + okno): kolejne numery przystanków bez duplikatów.
        await LockRouteAsync(input.DriverId, delivery.DeliveryDate, delivery.WindowStart, delivery.WindowEnd, ct);
        var next = (await RouteQuery(input.DriverId, delivery.DeliveryDate, delivery.WindowStart, delivery.WindowEnd)
            .MaxAsync(d => (int?)d.StopSequence, ct) ?? 0) + 1;

        DeliveryChange change;
        try { change = delivery.AssignTo(input.DriverId, next, by, _user.Email, DateTime.UtcNow); }
        catch (DeliveryDomainException ex) { return Result.Failure<DeliveryDto>(Error.Conflict(ex.Message)); }
        _db.DeliveryChanges.Add(change);
        return await SaveAsync(tx, () => DeliveryDto.From(delivery), ct);
    }

    public async Task<Result<DeliveryDto>> UnassignAsync(Guid storeId, Guid deliveryId, UnassignDriverInput input, CancellationToken ct)
    {
        if (!_user.ManagesStore(storeId)) return Result.Failure<DeliveryDto>(Error.Forbidden("Brak dostępu do dostaw tego sklepu."));
        if (_user.UserId is not Guid by) return Result.Failure<DeliveryDto>(Error.Unauthorized("Wymagane logowanie."));

        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        var delivery = await _db.Deliveries.FirstOrDefaultAsync(d => d.Id == deliveryId && d.StoreId == storeId, ct);
        if (delivery is null) return Result.Failure<DeliveryDto>(Error.NotFound("Dostawa nie istnieje w tym sklepie."));
        if (delivery.Status == DeliveryStatus.AvailableForPickup) return DeliveryDto.From(delivery); // już zdjęte
        if (delivery.Version != input.ExpectedVersion) return Stale<DeliveryDto>();

        DeliveryChange change;
        try { change = delivery.Unassign(by, _user.Email, DateTime.UtcNow); }
        catch (DeliveryDomainException ex) { return Result.Failure<DeliveryDto>(Error.Conflict(ex.Message)); }
        _db.DeliveryChanges.Add(change);
        return await SaveAsync(tx, () => DeliveryDto.From(delivery), ct);
    }

    /// <summary>
    /// Ręczna kolejność przystanków trasy (kierowca + dzień + okno). Operator wysyła WSZYSTKIE przystanki trasy
    /// z wersjami, które widział; inna zawartość trasy lub nowsza wersja któregoś przystanku → 409, bez zapisu.
    /// </summary>
    public async Task<Result<IReadOnlyList<DeliveryDto>>> ReorderRouteAsync(Guid storeId, RouteOrderInput input, CancellationToken ct)
    {
        if (!_user.ManagesStore(storeId))
            return Result.Failure<IReadOnlyList<DeliveryDto>>(Error.Forbidden("Brak dostępu do dostaw tego sklepu."));
        if (_user.UserId is not Guid by) return Result.Failure<IReadOnlyList<DeliveryDto>>(Error.Unauthorized("Wymagane logowanie."));
        if (input.Stops is null || input.Stops.Count == 0)
            return Result.Failure<IReadOnlyList<DeliveryDto>>(Error.Validation("Podaj przystanki trasy."));
        if (input.Stops.Select(s => s.DeliveryId).Distinct().Count() != input.Stops.Count)
            return Result.Failure<IReadOnlyList<DeliveryDto>>(Error.Validation("Przystanek powtarza się w trasie."));

        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        await LockRouteAsync(input.DriverId, input.DeliveryDate, input.WindowStart, input.WindowEnd, ct);
        var route = await RouteQuery(input.DriverId, input.DeliveryDate, input.WindowStart, input.WindowEnd)
            .Where(d => d.StoreId == storeId).ToListAsync(ct);

        var byId = route.ToDictionary(d => d.Id);
        if (route.Count != input.Stops.Count || input.Stops.Any(s => !byId.ContainsKey(s.DeliveryId)))
            return Stale<IReadOnlyList<DeliveryDto>>("Trasa zmieniła się w międzyczasie (inne przystanki) — odśwież widok.");
        if (input.Stops.Any(s => byId[s.DeliveryId].Version != s.ExpectedVersion))
            return Stale<IReadOnlyList<DeliveryDto>>();

        var now = DateTime.UtcNow;
        for (var i = 0; i < input.Stops.Count; i++)
        {
            var d = byId[input.Stops[i].DeliveryId];
            if (d.StopSequence == i + 1) continue;
            _db.DeliveryChanges.Add(d.SetStop(i + 1, by, _user.Email, now));
        }
        return await SaveAsync(tx, () => (IReadOnlyList<DeliveryDto>)input.Stops
            .Select(s => DeliveryDto.From(byId[s.DeliveryId])).ToList(), ct);
    }

    /// <summary>Historia dostawy (kto, kiedy: przypisanie, kolejność, odbiór, dostarczenie) — bez danych klienta.</summary>
    public async Task<Result<IReadOnlyList<DeliveryChangeDto>>> HistoryAsync(Guid storeId, Guid deliveryId, CancellationToken ct)
    {
        if (!_user.ManagesStore(storeId))
            return Result.Failure<IReadOnlyList<DeliveryChangeDto>>(Error.Forbidden("Brak dostępu do dostaw tego sklepu."));
        if (!await _db.Deliveries.AnyAsync(d => d.Id == deliveryId && d.StoreId == storeId, ct))
            return Result.Failure<IReadOnlyList<DeliveryChangeDto>>(Error.NotFound("Dostawa nie istnieje w tym sklepie."));
        var changes = await _db.DeliveryChanges.AsNoTracking().Where(c => c.DeliveryId == deliveryId)
            .OrderBy(c => c.Version).ToListAsync(ct);
        return changes.Select(DeliveryChangeDto.From).ToList();
    }

    // ================= Pomocnicze =================

    private IQueryable<Domain.Delivery> RouteQuery(Guid driverId, DateOnly? date, TimeOnly? start, TimeOnly? end)
        => _db.Deliveries.Where(d => d.DriverId == driverId && d.DeliveryDate == date && d.WindowStart == start
            && d.WindowEnd == end && (d.Status == DeliveryStatus.Assigned || d.Status == DeliveryStatus.InTransit));

    /// <summary>Blokada doradcza trasy do końca transakcji — zmiany jednej trasy wykonują się po kolei.</summary>
    private Task LockRouteAsync(Guid driverId, DateOnly? date, TimeOnly? start, TimeOnly? end, CancellationToken ct)
        => _db.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock(hashtextextended({$"route:{driverId}:{date}:{start}:{end}"}, 0))", ct);

    private async Task<Result<T>> SaveAsync<T>(Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction tx, Func<T> result, CancellationToken ct)
    {
        try
        {
            await _db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return result();
        }
        catch (DbUpdateException ex) when (IsConcurrentChange(ex))
        {
            return Stale<T>("Dostawa została właśnie zmieniona przez inną osobę — odśwież widok.");
        }
    }

    private static Result<T> Stale<T>(string? message = null) => Result.Failure<T>(Error.Conflict(
        message ?? "Dostawa została w międzyczasie zmieniona — odśwież widok i spróbuj ponownie."));
}
