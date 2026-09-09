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

/// <summary>Członek zespołu sklepu (pracownik/kierowca) — do panelu admina.</summary>
public sealed record TeamMemberDto(
    Guid Id, string Email, string FullName, string? Phone,
    bool IsActive, bool IsEmailVerified, string Role, Guid StoreId);
