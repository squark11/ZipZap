using Microsoft.Extensions.Logging;
using RabbitMQ.Client;

namespace ZipZap.BuildingBlocks.Messaging;

/// <summary>
/// Współdzielone, leniwie nawiązywane połączenie do RabbitMQ z ponawianiem
/// (broker może wstawać wolniej niż API). Singleton.
/// </summary>
public sealed class RabbitMqConnection : IDisposable
{
    private readonly RabbitMqOptions _options;
    private readonly ILogger<RabbitMqConnection> _logger;
    private readonly object _lock = new();
    private IConnection? _connection;

    public RabbitMqConnection(RabbitMqOptions options, ILogger<RabbitMqConnection> logger)
    {
        _options = options;
        _logger = logger;
    }

    public IConnection GetConnection()
    {
        if (_connection is { IsOpen: true }) return _connection;

        lock (_lock)
        {
            if (_connection is { IsOpen: true }) return _connection;

            var factory = new ConnectionFactory
            {
                HostName = _options.Host,
                Port = _options.Port,
                UserName = _options.User,
                Password = _options.Password,
                VirtualHost = _options.VirtualHost,
                DispatchConsumersAsync = true,
                AutomaticRecoveryEnabled = true,
            };

            for (var attempt = 1; ; attempt++)
            {
                try
                {
                    _connection = factory.CreateConnection("zipzap-monolith");
                    _logger.LogInformation("Połączono z RabbitMQ ({Host}:{Port}).", _options.Host, _options.Port);
                    return _connection;
                }
                catch (Exception ex) when (attempt <= 15)
                {
                    _logger.LogWarning("RabbitMQ niedostępny (próba {Attempt}): {Message}", attempt, ex.Message);
                    Thread.Sleep(TimeSpan.FromSeconds(3));
                }
            }
        }
    }

    public void Dispose() => _connection?.Dispose();
}
