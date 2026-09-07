using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ZipZap.BuildingBlocks.Outbox;

/// <summary>
/// Tło: cyklicznie odpytuje wszystkie procesory outboxa i publikuje zdarzenia.
/// Na start prosty polling; docelowo do wymiany na trigger/broker.
/// </summary>
public sealed class OutboxDispatcherHostedService : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(5);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<OutboxDispatcherHostedService> _logger;

    public OutboxDispatcherHostedService(
        IServiceScopeFactory scopeFactory,
        ILogger<OutboxDispatcherHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Dispatcher outboxa uruchomiony (co {Seconds}s).", PollInterval.TotalSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var processors = scope.ServiceProvider.GetServices<IOutboxProcessor>();
                foreach (var processor in processors)
                    await processor.ProcessPendingAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd w pętli dispatchera outboxa.");
            }

            try
            {
                await Task.Delay(PollInterval, stoppingToken);
            }
            catch (TaskCanceledException)
            {
                // zamykanie aplikacji — wyjdź cicho
            }
        }
    }
}
