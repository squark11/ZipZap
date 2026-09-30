using System.Threading.Channels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ZipZap.Modules.Identity.Application;

namespace ZipZap.Modules.Identity.Infrastructure;

/// <summary>
/// Prośby „nie pamiętam hasła" przetwarzane w tle: endpoint tylko przyjmuje adres, więc czas odpowiedzi nie zależy od
/// tego, czy konto istnieje (wyszukanie konta, zapis tokenu i kolejkowanie e-maila dzieją się poza żądaniem).
/// Przetwarzanie po kolei czyni też limit linków na konto odpornym na równoległe prośby.
/// </summary>
public sealed class PasswordResetRequestQueue : IPasswordResetRequestQueue
{
    private readonly Channel<string> _channel =
        Channel.CreateBounded<string>(new BoundedChannelOptions(1000) { FullMode = BoundedChannelFullMode.DropWrite });
    private readonly ILogger<PasswordResetRequestQueue> _logger;

    public PasswordResetRequestQueue(ILogger<PasswordResetRequestQueue> logger) => _logger = logger;

    internal ChannelReader<string> Reader => _channel.Reader;

    public void Enqueue(string? email)
    {
        if (string.IsNullOrWhiteSpace(email) || email.Length > 320) return;
        if (!_channel.Writer.TryWrite(email.Trim()))
            _logger.LogWarning("[RESET:queue] kolejka próśb o reset hasła pełna — prośba odrzucona.");
    }
}

public sealed class PasswordResetRequestWorker : BackgroundService
{
    private readonly PasswordResetRequestQueue _queue;
    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<PasswordResetRequestWorker> _logger;

    public PasswordResetRequestWorker(PasswordResetRequestQueue queue, IServiceScopeFactory scopes,
        ILogger<PasswordResetRequestWorker> logger)
    {
        _queue = queue;
        _scopes = scopes;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var email in _queue.Reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                await using var scope = _scopes.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<IdentityService>().ForgotPasswordAsync(email, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                // Bez adresu i treści — tylko typ błędu (np. chwilowa awaria bazy); użytkownik może poprosić ponownie.
                _logger.LogWarning("[RESET:queue] nie udało się przetworzyć prośby o reset hasła ({Category}).", ex.GetType().Name);
            }
        }
    }
}
