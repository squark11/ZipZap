using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ZipZap.Modules.Catalog.Application;
using ZipZap.Modules.Catalog.Domain;

namespace ZipZap.Modules.Catalog.Infrastructure;

/// <summary>
/// Retencja liczników wejść: raz na dobę zwija liczniki dzienne starsze niż
/// <see cref="StoreEntrySource.DailyRetentionDays"/> dni do sum miesięcznych. Bezpieczne przy kilku instancjach
/// (jedna atomowa instrukcja SQL — patrz <see cref="CatalogService.CompactEntryStatsAsync"/>).
/// </summary>
public sealed class StoreEntryRetentionService : BackgroundService
{
    private static readonly TimeSpan FirstRunDelay = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan Interval = TimeSpan.FromHours(24);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<StoreEntryRetentionService> _logger;

    public StoreEntryRetentionService(IServiceScopeFactory scopeFactory, ILogger<StoreEntryRetentionService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try { await Task.Delay(FirstRunDelay, stoppingToken); }
        catch (OperationCanceledException) { return; }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var keepFrom = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-StoreEntrySource.DailyRetentionDays);
                var monthly = await scope.ServiceProvider.GetRequiredService<CatalogService>()
                    .CompactEntryStatsAsync(keepFrom, stoppingToken);
                if (monthly > 0)
                    _logger.LogInformation(
                        "Liczniki wejść sprzed {KeepFrom} zwinięte do sum miesięcznych ({Rows} sum zaktualizowanych).",
                        keepFrom, monthly);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Retencja liczników wejść nie powiodła się ({ExceptionType}); ponowienie za dobę.",
                    ex.GetType().Name);
            }

            try { await Task.Delay(Interval, stoppingToken); }
            catch (OperationCanceledException) { return; }
        }
    }
}
