using Microsoft.EntityFrameworkCore;
using ZipZap.BuildingBlocks.Domain;
using ZipZap.BuildingBlocks.Messaging;
using ZipZap.BuildingBlocks.MultiTenancy;
using ZipZap.BuildingBlocks.Outbox;
using ZipZap.Contracts.Ordering;
using ZipZap.Modules.Ordering.Domain;
using ZipZap.Modules.Ordering.Infrastructure;

namespace ZipZap.Modules.Ordering.Application;

public enum OrderAction { Confirm, StartPicking, Ready, PickUp, Delivered, Complete, Cancel }

/// <summary>
/// Przypadki użycia Ordering. Dane produktu/sklepu bierze z lokalnego read-modelu
/// (budowanego ze zdarzeń Catalog) — bez sięgania do innego modułu.
/// </summary>
public sealed class OrderingService
{
    private readonly OrderingDbContext _db;
    private readonly ICurrentUser _user;
    private readonly IIntegrationEventTypeRegistry _events;

    public OrderingService(OrderingDbContext db, ICurrentUser user, IIntegrationEventTypeRegistry events)
    {
        _db = db;
        _user = user;
        _events = events;
    }

    // ---------------- Koszyk ----------------

    public async Task<Result<CartDto>> CreateCartAsync(Guid storeId, CancellationToken ct)
    {
        var store = await _db.CatalogStores.FirstOrDefaultAsync(s => s.Id == storeId, ct);
        if (store is null || !store.IsActive) return Error.NotFound("Sklep niedostępny.");

        var cart = Cart.Create(storeId, _user.UserId);
        _db.Carts.Add(cart);
        await _db.SaveChangesAsync(ct);
        return CartDto.From(cart);
    }

    public async Task<Result<CartDto>> GetCartAsync(Guid cartId, string token, CancellationToken ct)
    {
        var cart = await LoadCartAsync(cartId, ct);
        if (cart is null) return Error.NotFound("Koszyk nie istnieje.");
        if (!OwnsCart(cart, token)) return Error.Forbidden("Brak dostępu do koszyka.");
        return CartDto.From(cart);
    }

    public async Task<Result<CartDto>> AddItemAsync(Guid cartId, string token, Guid productId, int quantity, CancellationToken ct)
    {
        var cart = await LoadCartAsync(cartId, ct);
        if (cart is null) return Error.NotFound("Koszyk nie istnieje.");
        if (!OwnsCart(cart, token)) return Error.Forbidden("Brak dostępu do koszyka.");

        var product = await _db.CatalogProducts.FirstOrDefaultAsync(p => p.Id == productId, ct);
        if (product is null) return Error.NotFound("Produkt nie istnieje.");
        if (product.StoreId != cart.StoreId) return Error.Validation("Produkt należy do innego sklepu.");
        if (!product.IsAvailable) return Error.Validation("Produkt jest niedostępny.");

        try { cart.AddItem(product.Id, product.Name, product.Price, quantity); }
        catch (OrderingDomainException ex) { return Error.Validation(ex.Message); }

        await _db.SaveChangesAsync(ct);
        return CartDto.From(cart);
    }

    public async Task<Result<CartDto>> SetItemQuantityAsync(Guid cartId, string token, Guid productId, int quantity, CancellationToken ct)
    {
        var cart = await LoadCartAsync(cartId, ct);
        if (cart is null) return Error.NotFound("Koszyk nie istnieje.");
        if (!OwnsCart(cart, token)) return Error.Forbidden("Brak dostępu do koszyka.");

        try { cart.SetItemQuantity(productId, quantity); }
        catch (OrderingDomainException ex) { return Error.Validation(ex.Message); }

        await _db.SaveChangesAsync(ct);
        return CartDto.From(cart);
    }

    public async Task<Result<CartDto>> RemoveItemAsync(Guid cartId, string token, Guid productId, CancellationToken ct)
    {
        var cart = await LoadCartAsync(cartId, ct);
        if (cart is null) return Error.NotFound("Koszyk nie istnieje.");
        if (!OwnsCart(cart, token)) return Error.Forbidden("Brak dostępu do koszyka.");

        cart.RemoveItem(productId);
        await _db.SaveChangesAsync(ct);
        return CartDto.From(cart);
    }

    // ---------------- Strefy i sloty ----------------

    public async Task<Result<DeliveryZoneDto>> CreateZoneAsync(Guid storeId, string name, decimal fee, IEnumerable<string>? postalCodes, CancellationToken ct)
    {
        var guard = EnsureCanManageStore(storeId);
        if (guard.IsFailure) return guard.Error;

        DeliveryZone zone;
        try { zone = new DeliveryZone(storeId, name, fee, postalCodes); }
        catch (OrderingDomainException ex) { return Error.Validation(ex.Message); }

        _db.DeliveryZones.Add(zone);
        await _db.SaveChangesAsync(ct);
        return DeliveryZoneDto.From(zone);
    }

    public async Task<IReadOnlyList<DeliveryZoneDto>> ListZonesAsync(Guid storeId, CancellationToken ct)
        => await _db.DeliveryZones.AsNoTracking()
            .Where(z => z.StoreId == storeId && z.IsActive)
            .OrderBy(z => z.Name).Select(z => DeliveryZoneDto.From(z)).ToListAsync(ct);

    public async Task<Result<TimeSlotDto>> CreateSlotAsync(Guid storeId, Guid zoneId, DateOnly date,
        TimeOnly start, TimeOnly end, int maxOrders, CancellationToken ct)
    {
        var guard = EnsureCanManageStore(storeId);
        if (guard.IsFailure) return guard.Error;
        if (!await _db.DeliveryZones.AnyAsync(z => z.Id == zoneId && z.StoreId == storeId, ct))
            return Error.NotFound("Strefa dostaw nie istnieje w tym sklepie.");

        TimeSlot slot;
        try { slot = new TimeSlot(storeId, zoneId, date, start, end, maxOrders); }
        catch (OrderingDomainException ex) { return Error.Validation(ex.Message); }

        _db.TimeSlots.Add(slot);
        await _db.SaveChangesAsync(ct);
        return TimeSlotDto.From(slot);
    }

    public async Task<IReadOnlyList<TimeSlotDto>> ListAvailableSlotsAsync(Guid storeId, Guid? zoneId, DateOnly? date, CancellationToken ct)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var query = _db.TimeSlots.AsNoTracking()
            .Where(s => s.StoreId == storeId && s.Date >= today && s.ReservedCount < s.MaxOrders);
        if (zoneId.HasValue) query = query.Where(s => s.DeliveryZoneId == zoneId.Value);
        if (date.HasValue) query = query.Where(s => s.Date == date.Value);

        return await query.OrderBy(s => s.Date).ThenBy(s => s.StartTime)
            .Select(s => TimeSlotDto.From(s)).ToListAsync(ct);
    }

    // ---------------- Zamówienie ----------------

    public async Task<Result<OrderDto>> PlaceOrderAsync(Guid cartId, string token, Guid deliveryZoneId,
        Guid timeSlotId, string deliveryAddress, string contactPhone, CancellationToken ct)
    {
        if (_user.UserId is not Guid customerId)
            return Error.Unauthorized("Złożenie zamówienia wymaga zalogowania.");
        if (string.IsNullOrWhiteSpace(deliveryAddress)) return Error.Validation("Adres dostawy jest wymagany.");
        if (string.IsNullOrWhiteSpace(contactPhone)) return Error.Validation("Telefon kontaktowy jest wymagany.");

        var cart = await LoadCartAsync(cartId, ct);
        if (cart is null) return Error.NotFound("Koszyk nie istnieje.");
        if (!OwnsCart(cart, token)) return Error.Forbidden("Brak dostępu do koszyka.");
        if (cart.Status != CartStatus.Active) return Error.Validation("Koszyk nie jest już aktywny.");
        if (cart.Items.Count == 0) return Error.Validation("Koszyk jest pusty.");

        var store = await _db.CatalogStores.FirstOrDefaultAsync(s => s.Id == cart.StoreId, ct);
        if (store is null || !store.IsActive) return Error.Validation("Sklep jest niedostępny.");

        var zone = await _db.DeliveryZones.FirstOrDefaultAsync(z => z.Id == deliveryZoneId && z.StoreId == cart.StoreId, ct);
        if (zone is null || !zone.IsActive) return Error.NotFound("Strefa dostaw nie istnieje.");

        var slot = await _db.TimeSlots.FirstOrDefaultAsync(
            s => s.Id == timeSlotId && s.StoreId == cart.StoreId && s.DeliveryZoneId == deliveryZoneId, ct);
        if (slot is null) return Error.NotFound("Slot dostawy nie istnieje.");

        // Weryfikacja dostępności produktów (ceny bierzemy ze snapshotu koszyka).
        var productIds = cart.Items.Select(i => i.ProductId).ToList();
        var unavailable = await _db.CatalogProducts
            .Where(p => productIds.Contains(p.Id) && !p.IsAvailable)
            .Select(p => p.Name).ToListAsync(ct);
        if (unavailable.Count > 0)
            return Error.Validation($"Produkty niedostępne: {string.Join(", ", unavailable)}.");

        // Rezerwacja slotu (limit).
        try { slot.Reserve(); }
        catch (OrderingDomainException ex) { return Error.Conflict(ex.Message); }

        var lines = cart.Items
            .Select(i => new OrderLine(i.ProductId, i.ProductName, i.UnitPrice, i.Quantity))
            .ToList();

        var order = Order.Place(cart.StoreId, customerId, lines, store.CommissionRate, zone.DeliveryFee,
            deliveryZoneId, timeSlotId, deliveryAddress.Trim(), contactPhone.Trim());

        cart.AssignCustomer(customerId);
        cart.MarkCheckedOut();

        _db.Orders.Add(order);
        _db.AddOutboxMessage(new OrderPlaced(order.Id, order.StoreId, customerId,
            order.Subtotal, order.CommissionAmount, order.DeliveryFee, order.Total, timeSlotId), _events);

        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Error.Conflict("Wybrany slot został właśnie zapełniony. Wybierz inny termin.");
        }

        return OrderDto.From(order);
    }

    public async Task<Result<OrderDto>> GetOrderAsync(Guid orderId, CancellationToken ct)
    {
        var order = await LoadOrderAsync(orderId, ct);
        if (order is null) return Error.NotFound("Zamówienie nie istnieje.");

        var isOwner = _user.UserId == order.CustomerId;
        var isStaff = _user.Roles.Contains("Admin")
                      || (_user.Roles.Contains("StoreEmployee") && _user.StoreId == order.StoreId)
                      || _user.Roles.Contains("Driver");
        if (!isOwner && !isStaff) return Error.Forbidden("Brak dostępu do zamówienia.");

        return OrderDto.From(order);
    }

    public async Task<IReadOnlyList<OrderDto>> ListMyOrdersAsync(CancellationToken ct)
    {
        if (_user.UserId is not Guid customerId) return Array.Empty<OrderDto>();
        var orders = await _db.Orders.AsNoTracking()
            .Include(o => o.Items).Include(o => o.History)
            .Where(o => o.CustomerId == customerId)
            .OrderByDescending(o => o.PlacedAtUtc).ToListAsync(ct);
        return orders.Select(OrderDto.From).ToList();
    }

    public async Task<Result<IReadOnlyList<OrderDto>>> ListStoreOrdersAsync(Guid storeId, string? status, CancellationToken ct)
    {
        // Izolacja najemcy: tylko admin lub pracownik TEGO sklepu.
        var guard = EnsureCanManageStore(storeId);
        if (guard.IsFailure) return guard.Error;

        var query = _db.Orders.AsNoTracking().Include(o => o.Items).Include(o => o.History)
            .Where(o => o.StoreId == storeId);
        if (Enum.TryParse<OrderStatus>(status, ignoreCase: true, out var parsed))
            query = query.Where(o => o.Status == parsed);

        var orders = await query.OrderByDescending(o => o.PlacedAtUtc).ToListAsync(ct);
        return Result.Success<IReadOnlyList<OrderDto>>(orders.Select(OrderDto.From).ToList());
    }

    public async Task<Result<OrderDto>> ChangeStatusAsync(Guid orderId, OrderAction action, CancellationToken ct)
    {
        var order = await LoadOrderAsync(orderId, ct);
        if (order is null) return Error.NotFound("Zamówienie nie istnieje.");

        var authorize = Authorize(action, order);
        if (authorize.IsFailure) return authorize.Error;

        var by = _user.UserId;
        try
        {
            switch (action)
            {
                case OrderAction.Confirm: order.Confirm(by); break;
                case OrderAction.StartPicking: order.StartPicking(by); break;
                case OrderAction.Ready: order.MarkReadyForPickup(by); break;
                case OrderAction.PickUp: order.PickUp(by); break;
                case OrderAction.Delivered: order.MarkDelivered(by); break;
                case OrderAction.Complete: order.Complete(by); break;
                case OrderAction.Cancel: order.Cancel(by); break;
            }
        }
        catch (OrderingDomainException ex)
        {
            return Error.Conflict(ex.Message);
        }

        PublishStatusEvent(order, action);
        await _db.SaveChangesAsync(ct);
        return OrderDto.From(order);
    }

    // ---------------- Pomocnicze ----------------

    private Task<Cart?> LoadCartAsync(Guid cartId, CancellationToken ct)
        => _db.Carts.Include(c => c.Items).FirstOrDefaultAsync(c => c.Id == cartId, ct);

    private Task<Order?> LoadOrderAsync(Guid orderId, CancellationToken ct)
        => _db.Orders.Include(o => o.Items).Include(o => o.History).FirstOrDefaultAsync(o => o.Id == orderId, ct);

    private bool OwnsCart(Cart cart, string token)
        => cart.CartToken == token || (_user.UserId is Guid uid && cart.CustomerId == uid);

    private Result EnsureCanManageStore(Guid storeId)
    {
        if (_user.Roles.Contains("Admin")) return Result.Success();
        if (_user.Roles.Contains("StoreEmployee") && _user.StoreId == storeId) return Result.Success();
        return Result.Failure(Error.Forbidden("Brak uprawnień do zarządzania tym sklepem."));
    }

    private Result Authorize(OrderAction action, Order order)
    {
        var isAdmin = _user.Roles.Contains("Admin");
        var isStoreStaff = _user.Roles.Contains("StoreEmployee") && _user.StoreId == order.StoreId;
        var isDriver = _user.Roles.Contains("Driver");

        var allowed = action switch
        {
            OrderAction.PickUp or OrderAction.Delivered => isAdmin || isDriver,
            _ => isAdmin || isStoreStaff, // Confirm, StartPicking, Ready, Complete, Cancel
        };
        return allowed ? Result.Success() : Result.Failure(Error.Forbidden("Brak uprawnień do tej operacji."));
    }

    private void PublishStatusEvent(Order order, OrderAction action)
    {
        switch (action)
        {
            case OrderAction.Ready:
                _db.AddOutboxMessage(new OrderReadyForPickup(order.Id, order.StoreId), _events);
                break;
            case OrderAction.PickUp:
                _db.AddOutboxMessage(new OrderPickedUp(order.Id, order.StoreId), _events);
                break;
            case OrderAction.Delivered:
                _db.AddOutboxMessage(new OrderDelivered(order.Id, order.StoreId), _events);
                break;
            case OrderAction.Cancel:
                _db.AddOutboxMessage(new OrderCancelled(order.Id, order.StoreId), _events);
                break;
        }
    }
}
