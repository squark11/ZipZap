using System.Text.Json;
using RabbitMQ.Client;

namespace ZipZap.BuildingBlocks.Messaging;

/// <summary>
/// Szyna oparta o RabbitMQ (topic exchange). Publikuje zdarzenie z nazwą typu
/// w nagłówku (do deserializacji po stronie konsumenta). Zastępuje in-process
/// bus bez zmian w modułach — te wołają wyłącznie <see cref="IEventBus"/>.
/// </summary>
public sealed class RabbitMqEventBus : IEventBus
{
    private readonly RabbitMqConnection _connection;
    private readonly RabbitMqOptions _options;
    private readonly IIntegrationEventTypeRegistry _registry;

    public RabbitMqEventBus(RabbitMqConnection connection, RabbitMqOptions options, IIntegrationEventTypeRegistry registry)
    {
        _connection = connection;
        _options = options;
        _registry = registry;
    }

    public Task PublishAsync(IIntegrationEvent integrationEvent, CancellationToken ct = default)
    {
        var eventType = integrationEvent.GetType();
        var typeName = _registry.GetName(eventType);
        var body = JsonSerializer.SerializeToUtf8Bytes(integrationEvent, eventType);

        using var channel = _connection.GetConnection().CreateModel();
        channel.ExchangeDeclare(_options.Exchange, ExchangeType.Topic, durable: true);

        var props = channel.CreateBasicProperties();
        props.Type = typeName;
        props.MessageId = integrationEvent.Id.ToString();
        props.ContentType = "application/json";
        props.DeliveryMode = 2; // trwałe

        channel.BasicPublish(_options.Exchange, routingKey: typeName, basicProperties: props, body: body);
        return Task.CompletedTask;
    }
}
