using ZipZap.Modules.Delivery.Application;
using ZipZap.Modules.Identity.Application;
using ZipZap.Modules.Ordering.Application;

namespace ZipZap.Api.Configuration;

/// <summary>
/// Adapter portu <see cref="IDriverDirectory"/> nad modułem tożsamości: aktualny stan kont i przypisań kierowców
/// (z bazy, nie z tokenu). Moduł dostaw nie czyta tabel tożsamości bezpośrednio.
/// </summary>
public sealed class DriverDirectoryAdapter : IDriverDirectory
{
    private readonly IdentityService _identity;
    public DriverDirectoryAdapter(IdentityService identity) => _identity = identity;

    public async Task<IReadOnlyList<DriverSummary>> ListActiveDriversAsync(Guid storeId, CancellationToken ct)
        => (await _identity.ListActiveStoreDriversAsync(storeId, ct)).Select(d => new DriverSummary(d.Id, d.FullName)).ToList();

    public Task<bool> IsActiveDriverOfStoreAsync(Guid userId, Guid storeId, CancellationToken ct)
        => _identity.IsActiveDriverOfStoreAsync(userId, storeId, ct);
}

/// <summary>
/// Adapter portu <see cref="IOrderContactProvider"/> nad modułem zamówień: minimum danych do wydania i dostawy.
/// Wywoływany przez moduł dostaw WYŁĄCZNIE po sprawdzeniu uprawnień do konkretnych dostaw.
/// </summary>
public sealed class OrderContactAdapter : IOrderContactProvider
{
    private readonly OrderDeliveryInfoQuery _orders;
    public OrderContactAdapter(OrderDeliveryInfoQuery orders) => _orders = orders;

    public async Task<IReadOnlyDictionary<Guid, DeliveryContact>> GetAsync(IReadOnlyCollection<Guid> orderIds, CancellationToken ct)
        => (await _orders.GetAsync(orderIds, ct)).ToDictionary(kv => kv.Key, kv => new DeliveryContact(
            kv.Value.OrderCode, kv.Value.CustomerCode, kv.Value.DeliveryAddress, kv.Value.ContactPhone,
            kv.Value.DeliveryDate, kv.Value.WindowStart, kv.Value.WindowEnd, kv.Value.ItemCount, kv.Value.IsTestOrder));
}
