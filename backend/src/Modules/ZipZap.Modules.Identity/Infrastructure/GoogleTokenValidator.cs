using Google.Apis.Auth;
using Microsoft.Extensions.Logging;
using ZipZap.Modules.Identity.Application;

namespace ZipZap.Modules.Identity.Infrastructure;

/// <summary>
/// Weryfikuje token ID Google biblioteką Google.Apis.Auth (sprawdza podpis
/// kluczami Google, wystawcę i audience = aktualny Client ID z <see cref="IGoogleClientIdProvider"/>).
/// </summary>
public sealed class GoogleTokenValidator : IGoogleTokenValidator
{
    private readonly IGoogleClientIdProvider _clientIds;
    private readonly ILogger<GoogleTokenValidator> _logger;

    public GoogleTokenValidator(IGoogleClientIdProvider clientIds, ILogger<GoogleTokenValidator> logger)
    {
        _clientIds = clientIds;
        _logger = logger;
    }

    public async Task<GoogleUserInfo?> ValidateAsync(string idToken, CancellationToken ct = default)
    {
        var clientId = await _clientIds.GetClientIdAsync(ct);
        if (string.IsNullOrWhiteSpace(clientId))
        {
            _logger.LogWarning("Logowanie Google nie jest skonfigurowane (brak Client ID w panelu ani env).");
            return null;
        }
        if (string.IsNullOrWhiteSpace(idToken)) return null;

        try
        {
            var settings = new GoogleJsonWebSignature.ValidationSettings
            {
                Audience = new[] { clientId },
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
