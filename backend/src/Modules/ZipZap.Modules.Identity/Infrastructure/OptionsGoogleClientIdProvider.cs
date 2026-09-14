using Microsoft.Extensions.Options;
using ZipZap.Modules.Identity.Application;

namespace ZipZap.Modules.Identity.Infrastructure;

/// <summary>
/// Domyślne źródło Client ID: konfiguracja (env `Google:ClientId`).
/// Host nadpisuje je providerem czytającym magazyn panelu (env pozostaje fallbackiem).
/// </summary>
public sealed class OptionsGoogleClientIdProvider : IGoogleClientIdProvider
{
    private readonly GoogleOptions _options;
    public OptionsGoogleClientIdProvider(IOptions<GoogleOptions> options) => _options = options.Value;

    public Task<string?> GetClientIdAsync(CancellationToken ct = default)
        => Task.FromResult(string.IsNullOrWhiteSpace(_options.ClientId) ? null : _options.ClientId);
}
