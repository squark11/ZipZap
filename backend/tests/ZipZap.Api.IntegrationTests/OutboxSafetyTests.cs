using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;
using ZipZap.BuildingBlocks.Inbox;
using ZipZap.BuildingBlocks.Messaging;
using ZipZap.BuildingBlocks.Outbox;
using ZipZap.Modules.Notifications.Application;
using ZipZap.Modules.Notifications.Domain;

namespace ZipZap.Api.IntegrationTests;

/// <summary>
/// Inbox (tylko duplikat klucza jest „już obsłużone") i brak treści wyjątków / danych osobowych w logach —
/// na działającym hoście z przechwytywaniem WSZYSTKICH logów (także obiektów wyjątków).
/// </summary>
[Collection("outbox")]
public sealed class OutboxInboxAndLogSafetyTests
{
    private static readonly string[] Secrets =
        { OutboxProbeHandler.SecretInExceptionMessage, OutboxProbeHandler.TokenInExceptionMessage, "pii.w.ladunku@example.com" };

    private readonly OutboxApiFactory _f;
    public OutboxInboxAndLogSafetyTests(OutboxApiFactory f) => _f = f;

    [Fact]
    public async Task Failing_handler_and_dead_letter_leave_no_exception_text_or_payload_in_logs()
    {
        var failing = await OutboxTestData.AddProbeAsync(_f, failures: 1, oldest: DateTime.UtcNow.AddYears(-9));
        var dead = await OutboxTestData.AddMessageAsync(_f, "ZipZap.Tests.UnknownEvent",
            JsonSerializer.Serialize(new { email = "pii.w.ladunku@example.com" }), DateTime.UtcNow.AddYears(-9).AddSeconds(1));

        await DeliveryScenario.FlushOutboxAsync(_f);

        (await OutboxTestData.MessageAsync(_f, failing)).Error.Should().Be(OutboxErrorCategory.HandlerError);
        (await OutboxTestData.MessageAsync(_f, dead)).Error.Should().Be(OutboxErrorCategory.UnknownType);
        var logs = _f.Logs.Snapshot();
        logs.Should().Contain(l => l.Message.Contains(failing.ToString()) && l.Message.Contains(OutboxErrorCategory.HandlerError)
            && l.Message.Contains(nameof(InvalidOperationException)), "log ma identyfikator, kategorię i typ wyjątku");
        logs.Should().Contain(l => l.Message.Contains(dead.ToString()) && l.Message.Contains(OutboxErrorCategory.UnknownType));
        AssertNoSecrets(logs);
    }

    [Fact]
    public async Task Inbox_treats_only_a_duplicate_key_as_already_processed_other_database_errors_surface()
    {
        var key = Guid.NewGuid();
        await WithInboxAsync(i => i.MarkProcessedAsync(key, "test"));
        await WithInboxAsync(i => i.MarkProcessedAsync(key, "test")); // ten sam klucz drugi raz: oczekiwany duplikat
        (await WithInboxAsync(i => i.HasProcessedAsync(key))).Should().BeTrue();

        // Inny błąd bazy (tu: wartość za długa dla kolumny) NIE jest udawanym duplikatem — musi wyjść na zewnątrz.
        var other = Guid.NewGuid();
        var act = () => WithInboxAsync(i => i.MarkProcessedAsync(other, new string('x', 301)));
        (await act.Should().ThrowAsync<DbUpdateException>()).Which.Should().Match<DbUpdateException>(e => !EfInboxStore.IsDuplicateKey(e));
        (await WithInboxAsync(i => i.HasProcessedAsync(other))).Should().BeFalse();
    }

    [Fact]
    public async Task When_the_inbox_write_fails_the_event_is_retried_and_the_handler_effect_may_repeat()
    {
        var id = await OutboxTestData.AddProbeAsync(_f, failures: 0, oldest: DateTime.UtcNow.AddYears(-9).AddMinutes(1));
        var eventId = OutboxTestData.EventId(id);
        // Handler towarzyszący wykona skutek, ale zapis jego znacznika w inboxie się nie uda.
        FaultInjectingInboxStore.FailNextMark[IntegrationEventDispatcher.HandlerInboxKey(eventId, typeof(OutboxProbeCompanionHandler))] = true;

        await DeliveryScenario.FlushOutboxAsync(_f);
        var failed = await OutboxTestData.MessageAsync(_f, id);
        (failed.ProcessedAtUtc, failed.Attempts, failed.Error, failed.DeadLetteredAtUtc)
            .Should().Be(((DateTime?)null, 1, OutboxErrorCategory.Database, (DateTime?)null),
                "awaria zapisu znacznika jest widoczna jako błąd przejściowy — nie jest połykana ani udawana jako duplikat");
        OutboxProbeCompanionHandler.Handled[eventId].Should().Be(1);
        OutboxProbeHandler.Handled.ContainsKey(eventId).Should().BeFalse("dyspozycja przerwana po błędzie");

        await OutboxTestData.MakeDueAsync(_f, id);
        await DeliveryScenario.FlushOutboxAsync(_f);
        (await OutboxTestData.MessageAsync(_f, id)).ProcessedAtUtc.Should().NotBeNull();
        OutboxProbeHandler.Handled[eventId].Should().Be(1);
        OutboxProbeCompanionHandler.Handled[eventId].Should().Be(2,
            "OGRANICZENIE: skutek wykonany przed nieudanym zapisem znacznika powtarza się — co najmniej raz, nie dokładnie raz");
        AssertNoSecrets(_f.Logs.Snapshot());
    }

    private async Task<T> WithInboxAsync<T>(Func<EfInboxStore, Task<T>> action)
    {
        using var scope = _f.Services.CreateScope();
        return await action(scope.ServiceProvider.GetRequiredService<EfInboxStore>());
    }

    private async Task WithInboxAsync(Func<EfInboxStore, Task> action)
    {
        using var scope = _f.Services.CreateScope();
        await action(scope.ServiceProvider.GetRequiredService<EfInboxStore>());
    }

    internal static void AssertNoSecrets(IEnumerable<CapturedLog> logs, params string[] extra)
    {
        foreach (var secret in Secrets.Concat(extra))
            logs.Should().NotContain(l => l.Text.Contains(secret),
                "treść wyjątku, sekret ani dane z ładunku nie mogą trafić do logów ({0})", secret);
    }
}

/// <summary>Kanały bez bazy: pętla dispatchera, ścieżka RabbitMQ (nagłówki + logi), atrapa kanału powiadomień.</summary>
public sealed class OutboxChannelLogSafetyTests
{
    private const string Email = OutboxProbeHandler.SecretInExceptionMessage;
    private const string Token = OutboxProbeHandler.TokenInExceptionMessage;

    [Fact]
    public async Task Dispatcher_loop_failure_is_logged_with_category_and_type_only()
    {
        var logs = new CapturingLoggerProvider();
        var called = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var services = new ServiceCollection();
        services.AddLogging(b => b.AddProvider(logs));
        services.AddScoped<IOutboxProcessor>(_ => new ThrowingProcessor(called));
        await using var sp = services.BuildServiceProvider();

        var dispatcher = new OutboxDispatcherHostedService(sp.GetRequiredService<IServiceScopeFactory>(),
            new OutboxSignal(), sp.GetRequiredService<ILogger<OutboxDispatcherHostedService>>());
        await dispatcher.StartAsync(CancellationToken.None);
        await called.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await dispatcher.StopAsync(CancellationToken.None);

        var entries = logs.Snapshot();
        entries.Should().Contain(l => l.Level == LogLevel.Error && l.Message.Contains(OutboxErrorCategory.HandlerError)
            && l.Message.Contains(nameof(InvalidOperationException)));
        entries.Should().OnlyContain(l => l.Exception == null, "obiekt wyjątku (z treścią) nie trafia do loggera");
        OutboxInboxAndLogSafetyTests.AssertNoSecrets(entries);
    }

    [Fact]
    public void Broker_retry_and_dead_letter_headers_and_logs_carry_only_attempt_category_and_type()
    {
        var options = new RabbitMqOptions { MaxDeliveryAttempts = 3 };
        var ex = new InvalidOperationException($"SMTP 550 {Email} odrzucony, token={Token}");
        var logs = new CapturingLoggerProvider();
        using var factory = LoggerFactory.Create(b => b.AddProvider(logs));
        var logger = factory.CreateLogger("broker");

        var retry = BrokerFailurePolicy.Decide(ex, attempt: 0, options);
        var dead = BrokerFailurePolicy.Decide(ex, attempt: 2, options);
        (retry.DeadLetter, retry.NextAttempt, dead.DeadLetter, dead.NextAttempt).Should().Be((false, 1, true, 3));
        BrokerFailurePolicy.Log(logger, retry, "ZipZap.Contracts.Ordering.OrderPlaced", "msg-1", options.MaxDeliveryAttempts);
        BrokerFailurePolicy.Log(logger, dead, "ZipZap.Contracts.Ordering.OrderPlaced", "msg-1", options.MaxDeliveryAttempts);

        foreach (var d in new[] { retry, dead })
        {
            d.Headers.Keys.Should().BeEquivalentTo(BrokerFailurePolicy.AttemptHeader, BrokerFailurePolicy.ErrorCategoryHeader,
                BrokerFailurePolicy.ErrorTypeHeader);
            d.Headers[BrokerFailurePolicy.ErrorCategoryHeader].Should().Be(OutboxErrorCategory.HandlerError);
            d.Headers[BrokerFailurePolicy.ErrorTypeHeader].Should().Be(nameof(InvalidOperationException));
            d.Headers.Values.Select(v => v.ToString()!).Should().NotContain(v => v.Contains(Email) || v.Contains(Token));
        }
        var entries = logs.Snapshot();
        entries.Should().HaveCount(2).And.OnlyContain(l => l.Exception == null && l.Message.Contains("msg-1")
            && l.Message.Contains(OutboxErrorCategory.HandlerError) && l.Message.Contains(nameof(InvalidOperationException)));
        OutboxInboxAndLogSafetyTests.AssertNoSecrets(entries);
    }

    [Fact]
    public async Task Mock_notification_channel_does_not_log_the_payload()
    {
        var logs = new CapturingLoggerProvider();
        using var factory = LoggerFactory.Create(b => b.AddProvider(logs));
        var channel = new LoggingNotificationChannel(factory.CreateLogger<LoggingNotificationChannel>());

        await channel.SendAsync(new Notification(Guid.NewGuid(), "mock", "customer.welcome",
            JsonSerializer.Serialize(new { Email = "anna.nowak@example.com", FullName = "Anna Nowak" })));

        var entries = logs.Snapshot();
        entries.Should().ContainSingle(l => l.Message.Contains("customer.welcome"));
        OutboxInboxAndLogSafetyTests.AssertNoSecrets(entries, "anna.nowak@example.com", "Anna Nowak");
    }

    private sealed class ThrowingProcessor(TaskCompletionSource called) : IOutboxProcessor
    {
        public Task ProcessPendingAsync(CancellationToken ct = default)
        {
            called.TrySetResult();
            throw new InvalidOperationException($"Nie udało się dla {Email}, token={Token}.");
        }
    }
}
