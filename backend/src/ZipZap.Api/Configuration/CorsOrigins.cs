namespace ZipZap.Api.Configuration;

/// <summary>
/// Allowlista CORS a domeny IDN: przeglądarka wysyła nagłówek Origin z hostem w punycode
/// (https://app.dowózka.pl → https://app.xn--dowzka-dxa.pl). Do każdej pozycji z konfiguracji dokładamy jej postać
/// punycode, żeby wpis w wygodnym zapisie z polskimi znakami nie blokował aplikacji po cichu.
/// </summary>
public static class CorsOrigins
{
    public static string[] WithPunycode(IEnumerable<string> configured)
    {
        var result = new List<string>();
        foreach (var raw in configured)
        {
            var o = raw?.Trim().TrimEnd('/');
            if (string.IsNullOrEmpty(o)) continue;
            result.Add(o);
            if (Uri.TryCreate(o, UriKind.Absolute, out var uri) && uri.Host != uri.IdnHost)
                result.Add(CustomerAppUrl.Origin(uri));
        }
        return result.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }
}
