namespace ZipZap.BuildingBlocks.Logging;

/// <summary>
/// Maskowanie adresu e-mail do logów — nie zapisujemy pełnych danych osobowych (RODO/pilotaż).
/// Zostawia pierwszy znak lokalnej części i domenę, resztę zastępuje gwiazdkami.
/// Treści wiadomości (linki z tokenami weryfikacji/resetu) NIGDY nie trafiają do logów.
/// </summary>
public static class EmailLog
{
    public static string Mask(string? email)
    {
        if (string.IsNullOrWhiteSpace(email)) return "(brak)";
        var at = email.IndexOf('@');
        if (at <= 0) return "***";
        var local = email[..at];
        var domain = email[(at + 1)..];
        var head = local.Length <= 1 ? local : local[..1];
        return $"{head}***@{domain}";
    }
}
