using ZipZap.BuildingBlocks.Domain;

namespace ZipZap.Modules.Identity.Domain;

/// <summary>Użytkownik platformy (korzeń agregatu).</summary>
public sealed class User : AggregateRoot
{
    private readonly List<UserRole> _roles = new();

    public string Email { get; private set; } = default!;
    public string PasswordHash { get; private set; } = default!;
    public string FullName { get; private set; } = default!;
    public string? Phone { get; private set; }
    public bool IsActive { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    public IReadOnlyCollection<UserRole> Roles => _roles.AsReadOnly();

    private User() { } // EF

    private User(Guid id, string email, string passwordHash, string fullName, string? phone)
    {
        Id = id;
        Email = email;
        PasswordHash = passwordHash;
        FullName = fullName;
        Phone = phone;
        IsActive = true;
        CreatedAtUtc = DateTime.UtcNow;
    }

    public static User Register(
        string email,
        string passwordHash,
        string fullName,
        string? phone,
        Role role = Role.Customer,
        Guid? storeId = null)
    {
        var user = new User(Guid.NewGuid(), Normalize(email), passwordHash, fullName.Trim(), phone);
        user.AssignRole(role, storeId);
        return user;
    }

    public void AssignRole(Role role, Guid? storeId = null)
    {
        if (_roles.Any(r => r.Role == role && r.StoreId == storeId)) return;
        _roles.Add(new UserRole(Id, role, storeId));
    }

    public bool HasRole(Role role) => _roles.Any(r => r.Role == role);

    public void Deactivate() => IsActive = false;

    public static string Normalize(string email) => email.Trim().ToLowerInvariant();
}
