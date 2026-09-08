using Google.Apis.Auth;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ZipZap.Modules.Identity.Application;

namespace ZipZap.Modules.Identity.Infrastructure;

/// <summary>
/// Weryfikuje token ID Google biblioteką Google.Apis.Auth (sprawdza podpis
/// kluczami Google, wystawcę i audience = skonfigurowany ClientId).
/// </summary>
public sealed class GoogleTokenValidator : IGoogleTokenValidator
{
    private readonly GoogleOptions _options;
    private readonly ILogger<GoogleTokenValidator> _logger;

    public GoogleTokenValidator(IOptions<GoogleOptions> options, ILogger<GoogleTokenValidator> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task<GoogleUserInfo?> ValidateAsync(string idToken, CancellationToken ct = default)
    {
        if (!_options.Enabled)
        {
            _logger.LogWarning("Logowanie Google nie jest skonfigurowane (brak Google:ClientId).");
            return null;
        }
        if (string.IsNullOrWhiteSpace(idToken)) return null;

        try
        {
            var settings = new GoogleJsonWebSignature.ValidationSettings
            {
                Audience = new[] { _options.ClientId },
            };
            var payload = await GoogleJsonWebSignature.ValidateAsync(idToken, settings);
            return new GoogleUserInfo(payload.Subject, payload.Email, payload.Name ?? payload.Email, payload.EmailVerified);
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Nieprawidłowy token Google: {Message}", ex.Message);
            return null;
        }
    }
}
