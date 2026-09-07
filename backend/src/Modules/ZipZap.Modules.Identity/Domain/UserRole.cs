namespace ZipZap.Modules.Identity.Domain;

/// <summary>
/// Przypisanie roli do użytkownika, opcjonalnie w kontekście sklepu
/// (StoreId dla <see cref="Role.StoreEmployee"/> / <see cref="Role.Driver"/>).
/// </summary>
public sealed class UserRole
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid UserId { get; private set; }
    public Role Role { get; private set; }
    public Guid? StoreId { get; private set; }

    private UserRole() { } // EF

    public UserRole(Guid userId, Role role, Guid? storeId)
    {
        UserId = userId;
        Role = role;
        StoreId = storeId;
    }
}
