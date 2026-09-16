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
    public bool IsEmailVerified { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    /// <summary>Sekret TOTP (Base32) uwierzytelniania dwuskładnikowego. Null = 2FA nieskonfigurowane.</summary>
    public string? TwoFactorSecret { get; private set; }
    /// <summary>Czy 2FA jest aktywne (sekret potwierdzony kodem z aplikacji authenticator).</summary>
    public bool TwoFactorEnabled { get; private set; }

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

    public void ChangePassword(string newPasswordHash) => PasswordHash = newPasswordHash;

    /// <summary>Zapisuje (nowy) sekret TOTP — do czasu potwierdzenia kodem 2FA pozostaje wyłączone.</summary>
    public void SetTwoFactorSecret(string secret)
    {
        TwoFactorSecret = secret;
        TwoFactorEnabled = false;
    }

    /// <summary>Aktywuje 2FA po pomyślnej weryfikacji kodu (wymaga wcześniej ustawionego sekretu).</summary>
    public void EnableTwoFactor()
    {
        if (string.IsNullOrEmpty(TwoFactorSecret))
            throw new InvalidOperationException("Brak sekretu TOTP — najpierw rozpocznij konfigurację 2FA.");
        TwoFactorEnabled = true;
    }

    /// <summary>Wyłącza 2FA i usuwa sekret.</summary>
    public void DisableTwoFactor()
    {
        TwoFactorEnabled = false;
        TwoFactorSecret = null;
    }

    public void MarkEmailVerified() => IsEmailVerified = true;

    public void Deactivate() => IsActive = false;
    public void Activate() => IsActive = true;

    public static string Normalize(string email) => email.Trim().ToLowerInvariant();
}
