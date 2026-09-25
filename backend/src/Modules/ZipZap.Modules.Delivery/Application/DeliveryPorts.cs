namespace ZipZap.Modules.Delivery.Application;

/// <summary>Kierowca do przydziału (bez danych kontaktowych).</summary>
public sealed record DriverSummary(Guid Id, string FullName);

/// <summary>
/// Port: aktualny stan kierowców w module tożsamości (konto aktywne + przypisanie do sklepu).
/// Sprawdzany przy każdym dostępie do danych klienta — nie ufamy samym claimom tokenu, który może być
/// wydany przed dezaktywacją lub odpięciem od sklepu. Adapter dostarcza host.
/// </summary>
public interface IDriverDirectory
{
    Task<IReadOnlyList<DriverSummary>> ListActiveDriversAsync(Guid storeId, CancellationToken ct);
    Task<bool> IsActiveDriverOfStoreAsync(Guid userId, Guid storeId, CancellationToken ct);
}

/// <summary>Domyślnie brak kierowców (bezpiecznie: bez adaptera nikt nie dostaje danych klienta).</summary>
public sealed class NullDriverDirectory : IDriverDirectory
{
    public Task<IReadOnlyList<DriverSummary>> ListActiveDriversAsync(Guid storeId, CancellationToken ct)
        => Task.FromResult<IReadOnlyList<DriverSummary>>(Array.Empty<DriverSummary>());
    public Task<bool> IsActiveDriverOfStoreAsync(Guid userId, Guid storeId, CancellationToken ct) => Task.FromResult(false);
}

/// <summary>
/// Dane zamówienia potrzebne do wydania i dostawy. ADRES I TELEFON to dane osobowe — moduł dostaw ich nie
/// przechowuje ani nie loguje; pobiera je dopiero PO sprawdzeniu uprawnień do konkretnej dostawy.
/// </summary>
public sealed record DeliveryContact(
    string OrderCode, string CustomerCode, string Address, string Phone,
    DateOnly? DeliveryDate, TimeOnly? WindowStart, TimeOnly? WindowEnd, int ItemCount, bool IsTestOrder);

/// <summary>Port: dane dostawy zamówień (moduł zamówień). Adapter dostarcza host.</summary>
public interface IOrderContactProvider
{
    Task<IReadOnlyDictionary<Guid, DeliveryContact>> GetAsync(IReadOnlyCollection<Guid> orderIds, CancellationToken ct);
}

public sealed class NullOrderContactProvider : IOrderContactProvider
{
    public Task<IReadOnlyDictionary<Guid, DeliveryContact>> GetAsync(IReadOnlyCollection<Guid> orderIds, CancellationToken ct)
        => Task.FromResult<IReadOnlyDictionary<Guid, DeliveryContact>>(new Dictionary<Guid, DeliveryContact>());
}
