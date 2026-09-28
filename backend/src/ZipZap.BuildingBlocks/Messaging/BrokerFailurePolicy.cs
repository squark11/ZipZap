using Microsoft.Extensions.Logging;
using ZipZap.BuildingBlocks.Outbox;

namespace ZipZap.BuildingBlocks.Messaging;

/// <summary>Decyzja po nieudanej obsłudze wiadomości z brokera: ponowienie z opóźnieniem albo kolejka martwych.</summary>
public sealed record BrokerFailureDecision(
    int NextAttempt, bool DeadLetter, int DelayMs, string Category, string ExceptionType,
    IDictionary<string, object> Headers);

/// <summary>
/// Opcjonalna ścieżka RabbitMQ (tylko gdy ustawiono <c>RabbitMq:Host</c>): co trafia do nagłówków wiadomości
/// ponawianej/martwej i do logów. WYŁĄCZNIE numer próby, bezpieczna kategoria i nazwa typu wyjątku — nigdy
/// <c>Exception.Message</c> ani sam wyjątek (bywa w nich e-mail, fragment danych albo sekret).
/// </summary>
public static class BrokerFailurePolicy
{
    public const string AttemptHeader = "x-attempt";
    public const string ErrorCategoryHeader = "x-error-category";
    public const string ErrorTypeHeader = "x-error-type";

    public static BrokerFailureDecision Decide(Exception ex, int attempt, RabbitMqOptions options)
    {
        var next = attempt + 1;
        var dead = next >= options.MaxDeliveryAttempts;
        var delayMs = dead ? 0 : Math.Min(options.RetryBaseDelayMs * (int)Math.Pow(2, attempt), options.RetryMaxDelayMs);
        var category = OutboxErrorCategory.Classify(ex);
        var type = ex.GetType().Name;
        var headers = new Dictionary<string, object>
        {
            [AttemptHeader] = next,
            [ErrorCategoryHeader] = category,
            [ErrorTypeHeader] = type,
        };
        return new BrokerFailureDecision(next, dead, delayMs, category, type, headers);
    }

    public static void Log(ILogger logger, BrokerFailureDecision d, string typeName, string? messageId, int maxAttempts)
    {
        if (d.DeadLetter)
            logger.LogError(
                "Zdarzenie {MessageId} ({Type}) wyczerpało próby w brokerze ({Max}) → kolejka martwych; kategoria {Category} ({ExceptionType}).",
                messageId, typeName, maxAttempts, d.Category, d.ExceptionType);
        else
            logger.LogWarning(
                "Błąd obsługi zdarzenia {MessageId} ({Type}), próba {Attempt}, kategoria {Category} ({ExceptionType}) → ponowienie za {Delay} ms.",
                messageId, typeName, d.NextAttempt, d.Category, d.ExceptionType, d.DelayMs);
    }
}
