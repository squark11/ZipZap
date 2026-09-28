using System.Data.Common;
using System.Net.Http;
using System.Net.Sockets;
using System.Reflection;
using Microsoft.EntityFrameworkCore;

namespace ZipZap.BuildingBlocks.Outbox;

/// <summary>
/// Bezpieczne kategorie błędów outboxa. W polu <see cref="OutboxMessage.Error"/> zapisujemy WYŁĄCZNIE kod kategorii —
/// nigdy <c>Exception.Message</c> (bywa w nim adres e-mail, fragment danych albo sekret z konfiguracji) ani ładunek.
/// Szczegóły techniczne są w logach (typ wyjątku + identyfikator wiadomości, bez treści).
/// </summary>
public static class OutboxErrorCategory
{
    // Trwałe — ponowienie nic nie zmieni, wiadomość od razu do martwych.
    public const string UnknownType = "unknown_type";
    public const string UnreadablePayload = "unreadable_payload";

    // Przejściowe — ponawiane bez limitu (maks. co 30 min).
    public const string Database = "database";
    public const string Conflict = "conflict";
    public const string ExternalService = "external_service";
    public const string Timeout = "timeout";
    public const string HandlerError = "handler_error";

    /// <summary>Tylko do wyświetlania: wartość sprzed kategoryzacji (surowa treść wyjątku) — nie pokazujemy jej.</summary>
    public const string Legacy = "legacy";

    private static readonly IReadOnlyDictionary<string, string> Descriptions = new Dictionary<string, string>
    {
        [UnknownType] = "Nieznany typ zdarzenia — ta wersja aplikacji nie umie go obsłużyć.",
        [UnreadablePayload] = "Nieczytelna lub pusta treść zdarzenia.",
        [Database] = "Błąd bazy danych (np. chwilowa niedostępność).",
        [Conflict] = "Konflikt równoległej zmiany tych samych danych.",
        [ExternalService] = "Usługa zewnętrzna niedostępna (np. e-mail, push, broker).",
        [Timeout] = "Przekroczony czas oczekiwania.",
        [HandlerError] = "Błąd w obsłudze zdarzenia w module — szczegóły w logach (po identyfikatorze zdarzenia).",
        [Legacy] = "Błąd zapisany przed wprowadzeniem kategorii — treść ukryta.",
    };

    /// <summary>Kody zapisywane przez procesor (wszystko inne w bazie to wartość sprzed kategoryzacji).</summary>
    public static IReadOnlyCollection<string> StoredCodes { get; } =
        new[] { UnknownType, UnreadablePayload, Database, Conflict, ExternalService, Timeout, HandlerError };

    /// <summary>Kategoria do pokazania administratorowi; nieznana/stara wartość → <see cref="Legacy"/> (bez treści).</summary>
    public static string Normalize(string? stored)
        => stored is null ? string.Empty : StoredCodes.Contains(stored) ? stored : Legacy;

    public static string Describe(string category)
        => Descriptions.TryGetValue(category, out var d) ? d : string.Empty;

    /// <summary>Kategoria błędu przejściowego na podstawie TYPU wyjątku (treść nie jest czytana).</summary>
    public static string Classify(Exception ex)
    {
        for (Exception? e = Unwrap(ex); e is not null; e = e.InnerException)
        {
            switch (e)
            {
                case DbUpdateConcurrencyException: return Conflict;
                case DbUpdateException or DbException: return Database;
                case TimeoutException or OperationCanceledException: return Timeout;
                case HttpRequestException or SocketException or IOException: return ExternalService;
            }
            var ns = e.GetType().Namespace ?? string.Empty;
            if (ns.StartsWith("RabbitMQ", StringComparison.Ordinal) || ns.StartsWith("MailKit", StringComparison.Ordinal)
                || ns.StartsWith("MimeKit", StringComparison.Ordinal))
                return ExternalService;
        }
        return HandlerError;
    }

    private static Exception Unwrap(Exception ex) => ex switch
    {
        TargetInvocationException { InnerException: { } inner } => Unwrap(inner),
        AggregateException { InnerExceptions.Count: 1 } agg => Unwrap(agg.InnerExceptions[0]),
        _ => ex,
    };
}
