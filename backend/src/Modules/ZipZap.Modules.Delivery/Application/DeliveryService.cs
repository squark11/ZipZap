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
    DateTime CreatedAtUtc, DateTime? PickedUpAtUtc, DateTime? DeliveredAtUtc)
{
    public static DeliveryDto From(Domain.Delivery d) =>
        new(d.Id, d.OrderId, d.StoreId, d.DriverId, d.Status.ToString(), d.CreatedAtUtc, d.PickedUpAtUtc, d.DeliveredAtUtc);
}

/// <summary>Workflow kierowcy. Dostawa jest własnością kierowcy po przyjęciu z puli.</summary>
public sealed class DeliveryService
{
    private readonly DeliveryDbContext _db;
    private readonly ICurrentUser _user;
    private readonly IIntegrationEventTypeRegistry _events;

    public DeliveryService(DeliveryDbContext db, ICurrentUser user, IIntegrationEventTypeRegistry events)
    {
        _db = db;
        _user = user;
        _events = events;
    }

    /// <summary>
    /// Pula dostępnych dostaw — WYŁĄCZNIE ze sklepów, do których kierowca jest przypisany
    /// (claim <c>driver_store_id</c>). Admin widzi wszystkie. Brak przypisania = pusta lista.
    /// </summary>
    public async Task<IReadOnlyList<DeliveryDto>> ListAvailableAsync(CancellationToken ct)
    {
        var query = _db.Deliveries.AsNoTracking().Where(d => d.Status == DeliveryStatus.AvailableForPickup);
        if (!_user.Roles.Contains("Admin"))
        {
            var stores = _user.DriverStoreIds.ToArray();
            if (stores.Length == 0) return Array.Empty<DeliveryDto>();
            query = query.Where(d => stores.Contains(d.StoreId));
        }
        return await query.OrderBy(d => d.CreatedAtUtc).Select(d => DeliveryDto.From(d)).ToListAsync(ct);
    }

    public async Task<IReadOnlyList<DeliveryDto>> ListMineAsync(CancellationToken ct)
    {
        if (_user.UserId is not Guid driverId) return Array.Empty<DeliveryDto>();
        return await _db.Deliveries.AsNoTracking()
            .Where(d => d.DriverId == driverId)
            .OrderByDescending(d => d.CreatedAtUtc).Select(d => DeliveryDto.From(d)).ToListAsync(ct);
    }

    /// <summary>Wszystkie dostawy sklepu (widok operacyjny sklepu/administracji).</summary>
    public async Task<Result<IReadOnlyList<DeliveryDto>>> ListForStoreAsync(Guid storeId, CancellationToken ct)
    {
        if (!CanViewStore(storeId))
            return Result.Failure<IReadOnlyList<DeliveryDto>>(Error.Forbidden("Brak dostępu do dostaw tego sklepu."));

        var items = await _db.Deliveries.AsNoTracking()
            .Where(d => d.StoreId == storeId)
            .OrderByDescending(d => d.CreatedAtUtc)
            .Select(d => DeliveryDto.From(d))
            .ToListAsync(ct);
        return Result.Success<IReadOnlyList<DeliveryDto>>(items);
    }

    // Multi-lokalizacja: uwzględnij WSZYSTKIE sklepy użytkownika, nie tylko pierwszy (ManagesStore = StoreIds.Contains).
    private bool CanViewStore(Guid storeId) => _user.ManagesStore(storeId);

    public Task<Result<DeliveryDto>> AcceptAsync(Guid deliveryId, CancellationToken ct)
        => MutateAsync(deliveryId, requireOwner: false, apply: (d, driverId) => d.Accept(driverId), emit: null, ct);

    public Task<Result<DeliveryDto>> PickUpAsync(Guid deliveryId, CancellationToken ct)
        => MutateAsync(deliveryId, requireOwner: true, apply: (d, _) => d.MarkPickedUp(),
            emit: d => new OrderPickedUp(d.OrderId, d.StoreId), ct);

    public Task<Result<DeliveryDto>> DeliverAsync(Guid deliveryId, CancellationToken ct)
        => MutateAsync(deliveryId, requireOwner: true, apply: (d, _) => d.MarkDelivered(),
            emit: d => new OrderDelivered(d.OrderId, d.StoreId), ct);

    private async Task<Result<DeliveryDto>> MutateAsync(
        Guid deliveryId, bool requireOwner, Action<Domain.Delivery, Guid> apply,
        Func<Domain.Delivery, IIntegrationEvent>? emit, CancellationToken ct)
    {
        if (_user.UserId is not Guid driverId)
            return Result.Failure<DeliveryDto>(Error.Unauthorized("Wymagane logowanie kierowcy."));

        var delivery = await _db.Deliveries.FirstOrDefaultAsync(d => d.Id == deliveryId, ct);
        if (delivery is null) return Result.Failure<DeliveryDto>(Error.NotFound("Dostawa nie istnieje."));

        var isAdmin = _user.Roles.Contains("Admin");
        // Zakres: kierowca obsługuje tylko dostawy sklepów, do których jest przypisany — sprawdzane
        // PRZED jakąkolwiek zmianą stanu i przed emisją OrderPickedUp/OrderDelivered. Dotyczy też
        // odbioru/dostarczenia (np. po odpięciu kierowcy od sklepu).
        if (!_user.DrivesForStore(delivery.StoreId))
            return Result.Failure<DeliveryDto>(Error.Forbidden("Dostawa należy do sklepu, do którego nie jesteś przypisany."));
        if (requireOwner && !isAdmin && !delivery.IsOwnedBy(driverId))
            return Result.Failure<DeliveryDto>(Error.Forbidden("To nie jest Twoja dostawa."));

        try { apply(delivery, driverId); }
        catch (DeliveryDomainException ex) { return Result.Failure<DeliveryDto>(Error.Conflict(ex.Message)); }

        if (emit is not null) _db.AddOutboxMessage(emit(delivery), _events);

        try { await _db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { return Result.Failure<DeliveryDto>(Error.Conflict("Dostawa została właśnie przyjęta przez innego kierowcę.")); }

        return DeliveryDto.From(delivery);
    }
}
