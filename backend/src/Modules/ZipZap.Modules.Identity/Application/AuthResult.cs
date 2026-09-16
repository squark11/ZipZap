using ZipZap.Modules.Identity.Domain;

namespace ZipZap.Modules.Identity.Application;

public sealed record UserDto(Guid Id, string Email, string FullName, string[] Roles, Guid[] StoreIds)
{
    public static UserDto From(User user) => new(
        user.Id,
        user.Email,
        user.FullName,
        user.Roles.Select(r => r.Role.ToString()).Distinct().ToArray(),
        user.Roles.Where(r => r.StoreId.HasValue).Select(r => r.StoreId!.Value).Distinct().ToArray());
}

public sealed record AuthResult(
    string AccessToken,
    DateTime AccessTokenExpiresAtUtc,
    string RefreshToken,
    Guid RefreshTokenId,
    UserDto User);

/// <summary>
/// Wynik logowania: albo pełne uwierzytelnienie (tokeny), albo wyzwanie 2FA —
/// klient musi dokończyć logowanie kodem z aplikacji authenticator.
/// </summary>
public sealed record LoginResult(AuthResult? Auth, string? TwoFactorToken)
{
    public bool TwoFactorRequired => Auth is null;
    public static LoginResult Authenticated(AuthResult a) => new(a, null);
    public static LoginResult Challenge(string token) => new(null, token);
}

/// <summary>Dane do skonfigurowania 2FA — sekret (wpis ręczny) i URI otpauth (kod QR).</summary>
public sealed record TwoFactorSetupDto(string Secret, string OtpauthUri);

/// <summary>Członek zespołu sklepu (pracownik/kierowca) — do panelu admina.</summary>
public sealed record TeamMemberDto(
    Guid Id, string Email, string FullName, string? Phone,
    bool IsActive, bool IsEmailVerified, string Role, Guid StoreId);

/// <summary>Użytkownik platformy — tabela w panelu administratora serwisu.</summary>
public sealed record AdminUserDto(
    Guid Id, string Email, string FullName, string? Phone,
    bool IsActive, bool IsEmailVerified, string Roles, DateTime CreatedAtUtc);

/// <summary>Liczby użytkowników wg roli — do statystyk platformy.</summary>
public sealed record UserCounts(int Total, int Customers, int Stores, int Drivers, int Inactive);

/// <summary>Zapisane dane logowania konta testowego (ADMIN-ONLY; hasło jawne — konto testowe).</summary>
public sealed record TestCredentialDto(
    Guid Id, string Label, string Role, string Email, string Password, string? Note, DateTime UpdatedAtUtc)
{
    public static TestCredentialDto From(Domain.TestCredential c) =>
        new(c.Id, c.Label, c.Role, c.Email, c.Password, c.Note, c.UpdatedAtUtc);
}
