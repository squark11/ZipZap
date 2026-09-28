using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ZipZap.BuildingBlocks.Inbox;

namespace ZipZap.Api.IntegrationTests;

/// <summary>Wpis logu przechwycony w teście: sformatowana treść + pełny tekst wyjątku (jeśli przekazano obiekt).</summary>
public sealed record CapturedLog(string Category, LogLevel Level, string Message, string? Exception)
{
    public string Text => Exception is null ? Message : Message + "\n" + Exception;
}

/// <summary>Dostawca logów zbierający WSZYSTKO (także obiekt wyjątku) — do sprawdzania, czy dane nie wyciekają.</summary>
public sealed class CapturingLoggerProvider : ILoggerProvider
{
    public ConcurrentQueue<CapturedLog> Entries { get; } = new();

    public ILogger CreateLogger(string categoryName) => new Logger(categoryName, Entries);
    public void Dispose() { }

    public IReadOnlyList<CapturedLog> Snapshot() => Entries.ToList();

    private sealed class Logger(string category, ConcurrentQueue<CapturedLog> sink) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
            => sink.Enqueue(new CapturedLog(category, logLevel, formatter(state, exception), exception?.ToString()));
    }
}

/// <summary>
/// Inbox z wstrzykiwaną awarią zapisu znacznika (symulacja: skutek handlera zapisany, znacznik — nie).
/// Klucze do „zepsucia" ustawia test; każdy zawodzi raz.
/// </summary>
public sealed class FaultInjectingInboxStore(EfInboxStore inner) : IInboxStore
{
    public static readonly ConcurrentDictionary<Guid, bool> FailNextMark = new();

    public Task<bool> HasProcessedAsync(Guid messageId, CancellationToken ct = default) => inner.HasProcessedAsync(messageId, ct);

    public Task MarkProcessedAsync(Guid messageId, string type, CancellationToken ct = default)
        => FailNextMark.TryRemove(messageId, out _)
            ? throw new DbUpdateException("Symulowana awaria zapisu inboxa dla jan.kowalski@example.com.")
            : inner.MarkProcessedAsync(messageId, type, ct);
}
