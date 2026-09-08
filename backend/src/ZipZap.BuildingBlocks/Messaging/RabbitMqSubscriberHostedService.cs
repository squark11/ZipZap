using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using ZipZap.BuildingBlocks.Inbox;

namespace ZipZap.BuildingBlocks.Messaging;

/// <summary>
/// Konsument RabbitMQ z odpornością:
///  - deduplikacja przez inbox (Id zdarzenia) — chroni przed redostarczeniem,
///  - retry z wykładniczym backoffem przez kolejkę retry (TTL + dead-letter z powrotem),
///  - dead-letter queue po wyczerpaniu prób.
/// </summary>
public sealed class RabbitMqSubscriberHostedService : BackgroundService
{
    private const string AttemptHeader = "x-attempt";

    private readonly RabbitMqConnection _connection;
    private readonly RabbitMqOptions _options;
    private readonly IIntegrationEventTypeRegistry _registry;
    private readonly IntegrationEventDispatcher _dispatcher;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<RabbitMqSubscriberHostedService> _logger;
    private IModel? _channel;

    public RabbitMqSubscriberHostedService(
        RabbitMqConnection connection,
        RabbitMqOptions options,
        IIntegrationEventTypeRegistry registry,
        IntegrationEventDispatcher dispatcher,
        IServiceScopeFactory scopeFactory,
        ILogger<RabbitMqSubscriberHostedService> logger)
    {
        _connection = connection;
        _options = options;
        _registry = registry;
        _dispatcher = dispatcher;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var channel = _connection.GetConnection().CreateModel();
        _channel = channel;

        // --- Topologia ---
        channel.ExchangeDeclare(_options.Exchange, ExchangeType.Topic, durable: true);
        channel.ExchangeDeclare(_options.RetryExchange, ExchangeType.Fanout, durable: true);
        channel.ExchangeDeclare(_options.DeadExchange, ExchangeType.Fanout, durable: true);

        channel.QueueDeclare(_options.Queue, durable: true, exclusive: false, autoDelete: false);
        channel.QueueBind(_options.Queue, _options.Exchange, routingKey: "#");

        // Kolejka retry bez konsumenta: po TTL (per-wiadomość) dead-letteruje z powrotem do głównego exchange.
        var retryArgs = new Dictionary<string, object> { ["x-dead-letter-exchange"] = _options.Exchange };
        channel.QueueDeclare(_options.RetryQueue, durable: true, exclusive: false, autoDelete: false, arguments: retryArgs);
        channel.QueueBind(_options.RetryQueue, _options.RetryExchange, routingKey: "");

        channel.QueueDeclare(_options.DeadQueue, durable: true, exclusive: false, autoDelete: false);
        channel.QueueBind(_options.DeadQueue, _options.DeadExchange, routingKey: "");

        channel.BasicQos(prefetchSize: 0, prefetchCount: 10, global: false);

        var consumer = new AsyncEventingBasicConsumer(channel);
        consumer.Received += (_, ea) => HandleAsync(channel, ea, stoppingToken);
        channel.BasicConsume(_options.Queue, autoAck: false, consumer);

        _logger.LogInformation("Konsument RabbitMQ nasłuchuje {Queue} (retry: {Retry}, dead: {Dead}).",
            _options.Queue, _options.RetryQueue, _options.DeadQueue);
        return Task.CompletedTask;
    }

    private async Task HandleAsync(IModel channel, BasicDeliverEventArgs ea, CancellationToken ct)
    {
        var typeName = ea.BasicProperties.Type ?? string.Empty;
        var type = _registry.Resolve(typeName);
        if (type is null)
        {
            _logger.LogWarning("Nieznany typ zdarzenia z brokera: {Type}", typeName);
            channel.BasicAck(ea.DeliveryTag, multiple: false);
            return;
        }

        Guid.TryParse(ea.BasicProperties.MessageId, out var messageId);

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var inbox = scope.ServiceProvider.GetRequiredService<IInboxStore>();

            if (messageId != Guid.Empty && await inbox.HasProcessedAsync(messageId, ct))
            {
                channel.BasicAck(ea.DeliveryTag, multiple: false); // duplikat — pomiń
                return;
            }

            var @event = (IIntegrationEvent)JsonSerializer.Deserialize(ea.Body.Span, type)!;
            await _dispatcher.DispatchAsync(@event, ct);

            if (messageId != Guid.Empty)
                await inbox.MarkProcessedAsync(messageId, typeName, ct);

            channel.BasicAck(ea.DeliveryTag, multiple: false);
        }
        catch (Exception ex)
        {
            RouteToRetryOrDead(channel, ea, typeName, ex);
            channel.BasicAck(ea.DeliveryTag, multiple: false); // oryginał przekierowany
        }
    }

    private void RouteToRetryOrDead(IModel channel, BasicDeliverEventArgs ea, string typeName, Exception ex)
    {
        var attempt = ReadAttempt(ea.BasicProperties.Headers);
        var next = attempt + 1;

        var props = channel.CreateBasicProperties();
        props.Type = typeName;
        props.MessageId = ea.BasicProperties.MessageId;
        props.ContentType = "application/json";
        props.DeliveryMode = 2;
        var headers = new Dictionary<string, object> { [AttemptHeader] = next };

        if (next < _options.MaxDeliveryAttempts)
        {
            var delayMs = Math.Min(_options.RetryBaseDelayMs * (int)Math.Pow(2, attempt), _options.RetryMaxDelayMs);
            props.Expiration = delayMs.ToString();
            props.Headers = headers;
            channel.BasicPublish(_options.RetryExchange, routingKey: typeName, basicProperties: props, body: ea.Body);
            _logger.LogWarning(ex, "Błąd obsługi {Type} (próba {Attempt}) → retry za {Delay}ms.", typeName, next, delayMs);
        }
        else
        {
            headers["x-error"] = Encoding.UTF8.GetBytes(ex.Message);
            props.Headers = headers;
            channel.BasicPublish(_options.DeadExchange, routingKey: typeName, basicProperties: props, body: ea.Body);
            _logger.LogError(ex, "Wiadomość {Type} wyczerpała próby ({Max}) → dead-letter.", typeName, _options.MaxDeliveryAttempts);
        }
    }

    private static int ReadAttempt(IDictionary<string, object>? headers)
    {
        if (headers is null || !headers.TryGetValue(AttemptHeader, out var raw)) return 0;
        return raw switch
        {
            int i => i,
            long l => (int)l,
            byte[] b => int.TryParse(Encoding.UTF8.GetString(b), out var v) ? v : 0,
            string s => int.TryParse(s, out var v) ? v : 0,
            _ => 0,
        };
    }

    public override void Dispose()
    {
        _channel?.Dispose();
        base.Dispose();
    }
}
