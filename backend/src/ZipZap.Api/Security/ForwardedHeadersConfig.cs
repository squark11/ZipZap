using System.Net;
using Microsoft.AspNetCore.HttpOverrides;

namespace ZipZap.Api.Security;

/// <summary>
/// Konfiguracja zaufania do X-Forwarded-* (wpływa na IP klienta widziane przez limiter).
/// </summary>
public static class ForwardedHeadersConfig
{
    public static ForwardedHeadersOptions Build(IConfiguration cfg)
    {
        var options = new ForwardedHeadersOptions
        {
            ForwardedHeaders = ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedFor,
            // Tylko jeden przeskok: ostatni wpis dopisany przez proxy hostingu.
            ForwardLimit = 1,
        };

        options.KnownNetworks.Clear();
        options.KnownProxies.Clear();

        foreach (var p in cfg.GetSection("ForwardedHeaders:KnownProxies").Get<string[]>() ?? Array.Empty<string>())
            if (IPAddress.TryParse(p, out var ip)) options.KnownProxies.Add(ip);

        foreach (var n in cfg.GetSection("ForwardedHeaders:KnownNetworks").Get<string[]>() ?? Array.Empty<string>())
        {
            var parts = n.Split('/');
            if (parts.Length == 2 && IPAddress.TryParse(parts[0], out var prefix) && int.TryParse(parts[1], out var len))
                options.KnownNetworks.Add(new Microsoft.AspNetCore.HttpOverrides.IPNetwork(prefix, len));
        }

        return options;
    }
}
