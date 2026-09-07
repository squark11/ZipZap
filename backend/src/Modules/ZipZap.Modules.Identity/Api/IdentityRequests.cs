namespace ZipZap.Modules.Identity.Api;

public sealed record RegisterRequest(string Email, string Password, string FullName, string? Phone);

public sealed record LoginRequest(string Email, string Password);

public sealed record RefreshRequest(string RefreshToken);

public sealed record CreateUserRequest(
    string Email, string Password, string FullName, string? Phone, string Role, Guid? StoreId);

public sealed record UserResponse(Guid Id, string Email, string FullName, string[] Roles);

public sealed record AuthResponse(
    string AccessToken, DateTime ExpiresAtUtc, string RefreshToken, UserResponse User);
