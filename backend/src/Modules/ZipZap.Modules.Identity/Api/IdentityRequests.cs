namespace ZipZap.Modules.Identity.Api;

public sealed record RegisterRequest(string Email, string Password, string FullName, string? Phone, string? CaptchaToken = null);

public sealed record LoginRequest(string Email, string Password);

public sealed record RefreshRequest(string RefreshToken);

public sealed record GoogleLoginRequest(string IdToken);

public sealed record LogoutRequest(string? RefreshToken);

public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);

public sealed record SetActiveRequest(bool IsActive);

public sealed record VerifyEmailRequest(string Token);

public sealed record ForgotPasswordRequest(string Email);

public sealed record ResetPasswordRequest(string Token, string NewPassword);

public sealed record CreateUserRequest(
    string Email, string Password, string FullName, string? Phone, string Role, Guid? StoreId);

public sealed record AssignRoleRequest(string Role, Guid? StoreId = null);

public sealed record TestCredentialRequest(
    Guid? Id, string Label, string Role, string Email, string Password, string? Note = null);

public sealed record ComplianceFlagRequest(
    string SubjectType, Guid SubjectId, string SubjectLabel,
    string Category, string Severity, string? Note = null);

public sealed record ResolveFlagRequest(string? Resolution = null, bool Reopen = false);

public sealed record TwoFactorLoginRequest(string TwoFactorToken, string Code);

public sealed record TwoFactorCodeRequest(string Code);

public sealed record UserResponse(Guid Id, string Email, string FullName, string[] Roles, Guid[] StoreIds);

public sealed record AuthResponse(
    string AccessToken, DateTime ExpiresAtUtc, string RefreshToken, UserResponse User);

/// <summary>Odpowiedź logowania, gdy konto ma włączone 2FA — klient dokańcza przez /login/2fa.</summary>
public sealed record TwoFactorRequiredResponse(string TwoFactorToken)
{
    public bool TwoFactorRequired => true;
}

public sealed record TwoFactorSetupResponse(string Secret, string OtpauthUri);
