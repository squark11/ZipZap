namespace ZipZap.Modules.Identity.Domain;

/// <summary>
/// Zgłoszenie naruszenia regulaminu (narzędzie nadzoru administratora serwisu).
/// Dotyczy sklepu lub użytkownika; admin śledzi i rozwiązuje zgłoszenia.
/// </summary>
public sealed class ComplianceFlag
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public string SubjectType { get; private set; } = default!;   // "store" | "user"
    public Guid SubjectId { get; private set; }
    public string SubjectLabel { get; private set; } = default!;  // nazwa/e-mail (cache do wyświetlenia)
    public string Category { get; private set; } = default!;      // regulamin | platnosci | tresci | dostawa | inne
    public string Severity { get; private set; } = "medium";      // low | medium | high
    public string? Note { get; private set; }
    public string Status { get; private set; } = "Open";          // Open | Resolved
    public DateTime CreatedAtUtc { get; private set; } = DateTime.UtcNow;
    public DateTime? ResolvedAtUtc { get; private set; }
    public string? Resolution { get; private set; }

    private ComplianceFlag() { } // EF

    public ComplianceFlag(string subjectType, Guid subjectId, string subjectLabel,
        string category, string severity, string? note)
    {
        SubjectType = subjectType.Trim().ToLowerInvariant();
        SubjectId = subjectId;
        SubjectLabel = subjectLabel.Trim();
        Category = string.IsNullOrWhiteSpace(category) ? "inne" : category.Trim().ToLowerInvariant();
        Severity = Normalize(severity);
        Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
    }

    public void Resolve(string? resolution)
    {
        Status = "Resolved";
        ResolvedAtUtc = DateTime.UtcNow;
        Resolution = string.IsNullOrWhiteSpace(resolution) ? null : resolution.Trim();
    }

    public void Reopen()
    {
        Status = "Open";
        ResolvedAtUtc = null;
        Resolution = null;
    }

    private static string Normalize(string severity)
    {
        var s = (severity ?? "").Trim().ToLowerInvariant();
        return s is "low" or "medium" or "high" ? s : "medium";
    }
}
