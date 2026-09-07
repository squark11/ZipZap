using ZipZap.Modules.Identity.Domain;

namespace ZipZap.Modules.Identity.Application;

public readonly record struct AccessToken(string Value, DateTime ExpiresAtUtc);

public readonly record struct RefreshTokenValue(string Value, string Hash, DateTime ExpiresAtUtc);

public interface ITokenService
{
    AccessToken CreateAccessToken(User user);
    RefreshTokenValue CreateRefreshToken();
    string HashRefreshToken(string token);
}
