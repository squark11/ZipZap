using ZipZap.Modules.Identity.Application;
using ZipZap.Modules.Identity.Domain;

namespace ZipZap.Modules.Identity.Tests;

internal sealed class FakeGoogleTokenValidator : IGoogleTokenValidator
{
    private readonly GoogleUserInfo? _info;
    public FakeGoogleTokenValidator(GoogleUserInfo? info) => _info = info;
    public Task<GoogleUserInfo?> ValidateAsync(string idToken, CancellationToken ct = default) => Task.FromResult(_info);
}

internal sealed class FakeEmailSender : IEmailSender
{
    public Task SendAsync(EmailMessage message, CancellationToken ct = default) => Task.CompletedTask;
}

internal sealed class FakeTokenService : ITokenService
{
    public AccessToken CreateAccessToken(User user) => new("access-token", DateTime.UtcNow.AddMinutes(15));
    public RefreshTokenValue CreateRefreshToken()
    {
        var raw = Guid.NewGuid().ToString("N");
        return new RefreshTokenValue(raw, raw, DateTime.UtcNow.AddDays(14));
    }
    public string HashRefreshToken(string token) => token;
}
