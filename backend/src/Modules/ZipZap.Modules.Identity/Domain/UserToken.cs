namespace ZipZap.Modules.Identity.Domain;

public enum UserTokenType { EmailVerification, PasswordReset }

/// <summary>
/// Jednorazowy token (weryfikacja e-mail / reset hasła) przechowywany jako HASH,
/// z terminem ważności. Surowy token trafia tylko do e-maila.
/// </summary>
public sealed class UserToken
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid UserId { get; private set; }
    public UserTokenType Type { get; private set; }
    public string TokenHash { get; private set; } = default!;
    public DateTime ExpiresAtUtc { get; private set; }
    public DateTime CreatedAtUtc { get; private set; } = DateTime.UtcNow;
    public DateTime? UsedAtUtc { get; private set; }

    private UserToken() { } // EF

    public UserToken(Guid userId, UserTokenType type, string tokenHash, DateTime expiresAtUtc)
    {
        UserId = userId;
        Type = type;
        TokenHash = tokenHash;
        ExpiresAtUtc = expiresAtUtc;
    }

    public bool IsValid => UsedAtUtc is null && DateTime.UtcNow < ExpiresAtUtc;

    public void Use() => UsedAtUtc = DateTime.UtcNow;
}
