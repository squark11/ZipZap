using ZipZap.Modules.Identity.Application;

namespace ZipZap.Api.Configuration;

/// <summary>
/// Aktywny kanał poczty — ta sama kolejność co w <see cref="HttpEmailSender"/>: HTTP API dostawcy (env), potem SMTP
/// z panelu, potem SMTP z env; nic z tego = <see cref="EmailChannel.None"/> (atrapa: e-maile nie są wysyłane).
/// </summary>
public sealed class PlatformEmailChannelInfo : IEmailChannelInfo
{
    private readonly IConfiguration _cfg;
    private readonly PlatformIntegrationsStore _store;

    public PlatformEmailChannelInfo(IConfiguration cfg, PlatformIntegrationsStore store)
    {
        _cfg = cfg;
        _store = store;
    }

    public async Task<string> GetChannelAsync(CancellationToken ct = default)
    {
        if (!string.IsNullOrWhiteSpace(_cfg["Email:Http:ApiKey"]))
            return "http:" + (_cfg["Email:Http:Provider"] ?? "resend").Trim().ToLowerInvariant();
        if ((await _store.GetSmtpAsync(ct)).Enabled || !string.IsNullOrWhiteSpace(_cfg["Email:Smtp:Host"]))
            return "smtp";
        return EmailChannel.None;
    }
}
