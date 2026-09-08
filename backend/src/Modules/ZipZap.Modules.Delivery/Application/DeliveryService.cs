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

    public async Task<IReadOnlyList<DeliveryDto>> ListAvailableAsync(CancellationToken ct)
        => await _db.Deliveries.AsNoTracking()
            .Where(d => d.Status == DeliveryStatus.AvailableForPickup)
            .OrderBy(d => d.CreatedAtUtc).Select(d => DeliveryDto.From(d)).ToListAsync(ct);

    public async Task<IReadOnlyList<DeliveryDto>> ListMineAsync(CancellationToken ct)
    {
        if (_user.UserId is not Guid driverId) return Array.Empty<DeliveryDto>();
        return await _db.Deliveries.AsNoTracking()
            .Where(d => d.DriverId == driverId)
            .OrderByDescending(d => d.CreatedAtUtc).Select(d => DeliveryDto.From(d)).ToListAsync(ct);
    }

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
