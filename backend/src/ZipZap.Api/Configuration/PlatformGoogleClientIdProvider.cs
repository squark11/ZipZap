using Microsoft.Extensions.Options;
using ZipZap.Modules.Identity.Application;

namespace ZipZap.Api.Configuration;

/// <summary>
/// Adapter portu <see cref="IGoogleClientIdProvider"/>: Client ID z magazynu panelu,
/// a gdy pusty — fallback do env (`Google:ClientId`). Dzięki temu konfiguracja żyje w panelu,
/// a env pozostaje tylko wartością startową/awaryjną.
/// </summary>
public sealed class PlatformGoogleClientIdProvider : IGoogleClientIdProvider
{
    private readonly PlatformIntegrationsStore _store;
    private readonly GoogleOptions _envFallback;

    public PlatformGoogleClientIdProvider(PlatformIntegrationsStore store, IOptions<GoogleOptions> env)
    {
        _store = store;
        _envFallback = env.Value;
    }

    public async Task<string?> GetClientIdAsync(CancellationToken ct = default)
    {
        var i = await _store.GetAsync(ct);
        if (!string.IsNullOrWhiteSpace(i.GoogleClientId)) return i.GoogleClientId;
        return string.IsNullOrWhiteSpace(_envFallback.ClientId) ? null : _envFallback.ClientId;
    }
}
