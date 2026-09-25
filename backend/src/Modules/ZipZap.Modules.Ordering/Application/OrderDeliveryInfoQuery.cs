using Microsoft.EntityFrameworkCore;
using ZipZap.Modules.Ordering.Infrastructure;

namespace ZipZap.Modules.Ordering.Application;

/// <summary>
/// Minimum danych zamówienia do wydania i dostawy: kod, okno dostawy, adres, telefon, liczba pozycji, tryb testowy.
/// Bez treści pozycji, cen i kwot — kierowca nie dostaje całego zamówienia.
/// </summary>
public sealed record OrderDeliveryInfo(
    Guid OrderId, string OrderCode, string CustomerCode, string DeliveryAddress, string ContactPhone,
    DateOnly? DeliveryDate, TimeOnly? WindowStart, TimeOnly? WindowEnd, int ItemCount, bool IsTestOrder);

/// <summary>
/// Zapytanie dla ADAPTERA HOSTA (port modułu dostaw). Nie sprawdza uprawnień — robi to moduł dostaw
/// PRZED wywołaniem (przypisanie dostawy, sklep, aktywne konto). Nie wystawiać jako endpoint.
/// </summary>
public sealed class OrderDeliveryInfoQuery
{
    private readonly OrderingDbContext _db;

    public OrderDeliveryInfoQuery(OrderingDbContext db) => _db = db;

    public async Task<IReadOnlyDictionary<Guid, OrderDeliveryInfo>> GetAsync(IReadOnlyCollection<Guid> orderIds, CancellationToken ct)
    {
        if (orderIds.Count == 0) return new Dictionary<Guid, OrderDeliveryInfo>();
        var ids = orderIds.Distinct().ToList();
        var orders = await _db.Orders.AsNoTracking().Include(o => o.Items)
            .Where(o => ids.Contains(o.Id)).ToListAsync(ct);
        var slotIds = orders.Select(o => o.TimeSlotId).Distinct().ToList();
        var slots = await _db.TimeSlots.AsNoTracking().Where(s => slotIds.Contains(s.Id)).ToDictionaryAsync(s => s.Id, ct);

        return orders.ToDictionary(o => o.Id, o =>
        {
            slots.TryGetValue(o.TimeSlotId, out var slot);
            return new OrderDeliveryInfo(o.Id, ShoppingListBuilder.OrderCode(o.Id), ShoppingListBuilder.CustomerCode(o.CustomerId),
                o.DeliveryAddress, o.ContactPhone, slot?.Date, slot?.StartTime, slot?.EndTime, o.Items.Count, o.IsTestOrder);
        });
    }
}
