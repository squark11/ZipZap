namespace ZipZap.BuildingBlocks.MultiTenancy;

/// <summary>
/// Bieżący uwierzytelniony użytkownik (odczyt z JWT). Implementacja oparta o
/// HttpContext trafia do warstwy API wraz z modułem Identity.
/// </summary>
public interface ICurrentUser
{
    Guid? UserId { get; }
    Guid? StoreId { get; }
    IReadOnlyCollection<string> Roles { get; }
    bool IsAuthenticated { get; }
}
