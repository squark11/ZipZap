namespace ZipZap.BuildingBlocks.Messaging;

/// <summary>
/// Konfiguracja brokera. Brak <see cref="Host"/> = broker wyłączony (szyna in-process).
/// W docker-compose ustawiane przez zmienną RabbitMq__Host=rabbitmq.
/// </summary>
public sealed class RabbitMqOptions
{
    public const string SectionName = "RabbitMq";

    public string? Host { get; set; }
    public int Port { get; set; } = 5672;
    public string User { get; set; } = "guest";
    public string Password { get; set; } = "guest";
    public string VirtualHost { get; set; } = "/";
    public string Exchange { get; set; } = "zipzap.events";
    public string Queue { get; set; } = "zipzap.monolith";

    // Odporność: retry z backoffem + dead-letter.
    public string RetryExchange { get; set; } = "zipzap.events.retry";
    public string RetryQueue { get; set; } = "zipzap.monolith.retry";
    public string DeadExchange { get; set; } = "zipzap.events.dead";
    public string DeadQueue { get; set; } = "zipzap.monolith.dead";
    public int MaxDeliveryAttempts { get; set; } = 5;
    public int RetryBaseDelayMs { get; set; } = 5000;
    public int RetryMaxDelayMs { get; set; } = 60000;

    public bool Enabled => !string.IsNullOrWhiteSpace(Host);
}
