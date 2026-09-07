namespace ZipZap.BuildingBlocks.MultiTenancy;

/// <summary>
/// Bieżący najemca (sklep). Multi-tenancy row-level: każda encja domenowa
/// niesie StoreId, a moduły filtrują dane po tej wartości.
/// </summary>
public interface ICurrentTenant
{
    Guid? StoreId { get; }
    bool HasTenant { get; }
}

/// <summary>
/// Mutowalna implementacja ustawiana per-request (np. z claimu JWT lub nagłówka).
/// Rejestrowana jako scoped.
/// </summary>
public sealed class CurrentTenant : ICurrentTenant
{
    public Guid? StoreId { get; private set; }
    public bool HasTenant => StoreId.HasValue;

    public void Set(Guid? storeId) => StoreId = storeId;
}
