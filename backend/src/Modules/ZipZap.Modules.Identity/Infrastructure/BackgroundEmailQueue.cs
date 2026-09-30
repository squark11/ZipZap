using System.Threading.Channels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ZipZap.BuildingBlocks.Logging;
using ZipZap.Modules.Identity.Application;

namespace ZipZap.Modules.Identity.Infrastructure;

/// <summary>Stan dostarczania poczty z kolejki (dla administratora) — bez adresów i treści.</summary>
public sealed class EmailDeliveryStatus
{
    private long _sent, _failed, _dropped;
    public DateTime? LastSentAtUtc { get; private set; }
    public DateTime? LastFailureAtUtc { get; private set; }
    public string? LastFailureCategory { get; private set; }
    public long Sent => Interlocked.Read(ref _sent);
    public long Failed => Interlocked.Read(ref _failed);
    public long Dropped => Interlocked.Read(ref _dropped);

    internal void MarkSent() { Interlocked.Increment(ref _sent); LastSentAtUtc = DateTime.UtcNow; }
    internal void MarkFailed(string category) { Interlocked.Increment(ref _failed); LastFailureAtUtc = DateTime.UtcNow; LastFailureCategory = category; }
    internal void MarkDropped() => Interlocked.Increment(ref _dropped);
}

/// <summary>Kolejka w pamięci (ograniczona) — przepełnienie odrzuca nową wiadomość zamiast blokować żądanie.</summary>
public sealed class BackgroundEmailQueue : IEmailQueue
{
    private readonly Channel<EmailMessage> _channel =
        Channel.CreateBounded<EmailMessage>(new BoundedChannelOptions(500) { FullMode = BoundedChannelFullMode.DropWrite });
    private readonly EmailDeliveryStatus _status;
    private readonly ILogger<BackgroundEmailQueue> _logger;

    public BackgroundEmailQueue(EmailDeliveryStatus status, ILogger<BackgroundEmailQueue> logger)
    {
        _status = status;
        _logger = logger;
    }

    internal ChannelReader<EmailMessage> Reader => _channel.Reader;

    public void Enqueue(EmailMessage message)
    {
        if (_channel.Writer.TryWrite(message)) return;
        _status.MarkDropped();
        _logger.LogWarning("[EMAIL:queue] kolejka pełna — wiadomość odrzucona. to={To} subject={Subject}",
            EmailLog.Mask(message.To), message.Subject);
    }
}

/// <summary>
/// Wysyła wiadomości z <see cref="BackgroundEmailQueue"/>: 3 próby (od razu, po 5 s, po 30 s). Loguje wyłącznie
/// zamaskowany adres, temat i TYP wyjątku — nigdy treści (linki z tokenami) ani komunikatu dostawcy.
/// </summary>
public sealed class EmailQueueWorker : BackgroundService
{
    private static readonly TimeSpan[] Backoff = [TimeSpan.Zero, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(30)];

    private readonly BackgroundEmailQueue _queue;
    private readonly IServiceScopeFactory _scopes;
    private readonly EmailDeliveryStatus _status;
    private readonly ILogger<EmailQueueWorker> _logger;

    public EmailQueueWorker(BackgroundEmailQueue queue, IServiceScopeFactory scopes, EmailDeliveryStatus status,
        ILogger<EmailQueueWorker> logger)
    {
        _queue = queue;
        _scopes = scopes;
        _status = status;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var message in _queue.Reader.ReadAllAsync(stoppingToken))
            await SendWithRetryAsync(message, stoppingToken);
    }

    private async Task SendWithRetryAsync(EmailMessage message, CancellationToken ct)
    {
        for (var attempt = 0; attempt < Backoff.Length; attempt++)
        {
            if (Backoff[attempt] > TimeSpan.Zero) await Task.Delay(Backoff[attempt], ct);
            try
            {
                await using var scope = _scopes.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<IEmailSender>().SendAsync(message, ct);
                _status.MarkSent();
                return;
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                var category = ex.GetType().Name;
                _status.MarkFailed(category);
                _logger.LogWarning("[EMAIL:queue] wysyłka nieudana (próba {Attempt}/{Max}, {Category}). to={To} subject={Subject}",
                    attempt + 1, Backoff.Length, category, EmailLog.Mask(message.To), message.Subject);
            }
        }
    }
}
