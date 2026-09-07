using System.Text.Json;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace ZipZap.BuildingBlocks.Messaging;

/// <summary>
/// Konsument RabbitMQ: nasłuchuje kolejki monolitu (bind "#" — wszystkie zdarzenia),
/// deserializuje po nazwie typu z nagłówka i rozsyła do handlerów przez
/// <see cref="IntegrationEventDispatcher"/>. ACK po sukcesie, NACK (bez requeue)
/// przy błędzie, by uniknąć pętli (docelowo dead-letter).
/// </summary>
public sealed class RabbitMqSubscriberHostedService : BackgroundService
{
    private readonly RabbitMqConnection _connection;
    private readonly RabbitMqOptions _options;
    private readonly IIntegrationEventTypeRegistry _registry;
    private readonly IntegrationEventDispatcher _dispatcher;
    private readonly ILogger<RabbitMqSubscriberHostedService> _logger;
    private IModel? _channel;

    public RabbitMqSubscriberHostedService(
        RabbitMqConnection connection,
        RabbitMqOptions options,
        IIntegrationEventTypeRegistry registry,
        IntegrationEventDispatcher dispatcher,
        ILogger<RabbitMqSubscriberHostedService> logger)
    {
        _connection = connection;
        _options = options;
        _registry = registry;
        _dispatcher = dispatcher;
        _logger = logger;
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var channel = _connection.GetConnection().CreateModel();
        _channel = channel;

        channel.ExchangeDeclare(_options.Exchange, ExchangeType.Topic, durable: true);
        channel.QueueDeclare(_options.Queue, durable: true, exclusive: false, autoDelete: false);
        channel.QueueBind(_options.Queue, _options.Exchange, routingKey: "#");
        channel.BasicQos(prefetchSize: 0, prefetchCount: 10, global: false);

        var consumer = new AsyncEventingBasicConsumer(channel);
        consumer.Received += async (_, ea) =>
        {
            try
            {
                var typeName = ea.BasicProperties.Type ?? string.Empty;
                var type = _registry.Resolve(typeName);
                if (type is null)
                {
                    _logger.LogWarning("Nieznany typ zdarzenia z brokera: {Type}", typeName);
                    channel.BasicAck(ea.DeliveryTag, multiple: false);
                    return;
                }

                var @event = (IIntegrationEvent)JsonSerializer.Deserialize(ea.Body.Span, type)!;
                await _dispatcher.DispatchAsync(@event, stoppingToken);
                channel.BasicAck(ea.DeliveryTag, multiple: false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd obsługi wiadomości z RabbitMQ.");
                channel.BasicNack(ea.DeliveryTag, multiple: false, requeue: false);
            }
        };

        channel.BasicConsume(_options.Queue, autoAck: false, consumer);
        _logger.LogInformation("Konsument RabbitMQ nasłuchuje kolejki {Queue}.", _options.Queue);
        return Task.CompletedTask;
    }

    public override void Dispose()
    {
        _channel?.Dispose();
        base.Dispose();
    }
}
