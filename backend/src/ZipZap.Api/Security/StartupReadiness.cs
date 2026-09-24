using System.Collections.Concurrent;

namespace ZipZap.Api.Security;

/// <summary>
/// Warunki gotowości wyznaczane przy starcie (nieudana migracja modułu, konto administratora
/// z domyślnym hasłem w trybie publicznym). Gdy lista nie jest pusta, <c>/health/ready</c>
/// zwraca 503 — usługa nie zgłasza gotowości. Szczegóły tylko w logach (bez sekretów).
/// </summary>
public sealed class StartupReadiness
{
    private readonly ConcurrentQueue<string> _problems = new();

    /// <summary>Kody problemów (bez danych wrażliwych), np. "migration:Identity", "admin-default-password".</summary>
    public IReadOnlyCollection<string> Problems => _problems.ToArray();

    public bool IsReady => _problems.IsEmpty;

    public void Report(string code) => _problems.Enqueue(code);
}
