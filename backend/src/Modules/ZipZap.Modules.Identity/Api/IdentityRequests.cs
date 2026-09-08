namespace ZipZap.Modules.Identity.Api;

public sealed record RegisterRequest(string Email, string Password, string FullName, string? Phone);

public sealed record LoginRequest(string Email, string Password);

public sealed record RefreshRequest(string RefreshToken);

public sealed record LogoutRequest(string? RefreshToken);

public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);

public sealed record SetActiveRequest(bool IsActive);

public sealed record VerifyEmailRequest(string Token);

public sealed record ForgotPasswordRequest(string Email);

public sealed record ResetPasswordRequest(string Token, string NewPassword);

public sealed record CreateUserRequest(
    string Email, string Password, string FullName, string? Phone, string Role, Guid? StoreId);

public sealed record UserResponse(Guid Id, string Email, string FullName, string[] Roles);

public sealed record AuthResponse(
    string AccessToken, DateTime ExpiresAtUtc, string RefreshToken, UserResponse User);
