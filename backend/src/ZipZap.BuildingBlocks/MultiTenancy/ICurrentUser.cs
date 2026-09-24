namespace ZipZap.BuildingBlocks.MultiTenancy;

/// <summary>
/// Bieżący uwierzytelniony użytkownik (odczyt z JWT). Implementacja oparta o
/// HttpContext trafia do warstwy API wraz z modułem Identity.
/// </summary>
public interface ICurrentUser
{
    Guid? UserId { get; }

    /// <summary>Pierwszy sklep użytkownika (zgodność wsteczna). Do autoryzacji użyj <see cref="ManagesStore"/>.</summary>
    Guid? StoreId { get; }

    /// <summary>Wszystkie sklepy, których użytkownik jest pracownikiem (multi-lokalizacja).</summary>
    IReadOnlyCollection<Guid> StoreIds { get; }

    IReadOnlyCollection<string> Roles { get; }
    bool IsAuthenticated { get; }

    /// <summary>Czy może zarządzać danym sklepem: Admin lub pracownik TEGO sklepu (także jednej z wielu lokalizacji).</summary>
    bool ManagesStore(Guid storeId) => Roles.Contains("Admin") || StoreIds.Contains(storeId);

    /// <summary>
    /// Sklepy, do których użytkownik jest przypisany jako KIEROWCA (claim <c>driver_store_id</c>).
    /// Nie dają uprawnień zarządczych — tylko prawo obsługi dostaw tych sklepów.
    /// </summary>
    IReadOnlyCollection<Guid> DriverStoreIds => Array.Empty<Guid>();

    /// <summary>Czy może obsługiwać dostawy danego sklepu: Admin lub kierowca przypisany do TEGO sklepu.</summary>
    bool DrivesForStore(Guid storeId) => Roles.Contains("Admin") || DriverStoreIds.Contains(storeId);
}
