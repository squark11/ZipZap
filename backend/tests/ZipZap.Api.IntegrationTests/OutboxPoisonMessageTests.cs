using System.Collections.Concurrent;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using ZipZap.BuildingBlocks.Messaging;
using ZipZap.BuildingBlocks.Outbox;
using ZipZap.Contracts.Ordering;
using ZipZap.Modules.Ordering.Infrastructure;

namespace ZipZap.Api.IntegrationTests;

/// <summary>Zdarzenie testowe outboxa (rejestrowane tylko w <see cref="OutboxApiFactory"/>).</summary>
public sealed record OutboxProbeEvent(string Tag) : IntegrationEvent;

/// <summary>Handler testowy: dla danego zdarzenia zawodzi zadaną liczbę razy (przejściowa awaria), potem działa.</summary>
public sealed class OutboxProbeHandler : IIntegrationEventHandler<OutboxProbeEvent>
{
    public static readonly ConcurrentDictionary<Guid, int> FailuresLeft = new();
    public static readonly ConcurrentDictionary<Guid, int> Handled = new();

    public Task HandleAsync(OutboxProbeEvent e, CancellationToken ct = default)
    {
        if (FailuresLeft.TryGetValue(e.Id, out var left) && left > 0)
        {
            FailuresLeft[e.Id] = left - 1;
            throw new InvalidOperationException("Symulowana przejściowa awaria handlera.");
        }
        Handled.AddOrUpdate(e.Id, 1, (_, n) => n + 1);
        return Task.CompletedTask;
    }
}

/// <summary>
/// Outbox: błędy TRWAŁE (nieznany typ, nieczytelny ładunek) → od razu do martwych; błędy PRZEJŚCIOWE → ponawiane
/// bez limitu z narastającym odstępem (maks. 30 min), nigdy odkładane. Żadna nieudana wiadomość nie blokuje kolejnych.
/// Przetwarzanie ręczne (dispatcher w tle wyłączony) — przez procesory WSZYSTKICH modułów.
/// </summary>
[Collection("outbox")]
public sealed class OutboxPoisonMessageTests
{
    private const int BatchSize = OutboxProcessor<OrderingDbContext>.BatchSize;
    private static readonly TimeSpan MaxDelay = OutboxProcessor<OrderingDbContext>.MaxRetryDelay;

    private readonly OutboxApiFactory _f;
    public OutboxPoisonMessageTests(OutboxApiFactory f) => _f = f;

    [Fact]
    public async Task Poison_messages_filling_a_whole_batch_do_not_block_a_later_OrderReadyForPickup()
    {
        var s = await OrderScenario.StoreWithSlotAsync(_f);
        // Starsze niż wszystko, co wygeneruje zamówienie, i więcej niż jedna partia — dawniej pierwsza partia
        // na zawsze składała się z samych trujących wiadomości, a dostawa nigdy nie powstawała.
        var poison = await AddPoisonAsync(BatchSize + 5, oldest: DateTime.UtcNow.AddDays(-1));

        var ready = await DeliveryScenario.ReadyOrderAsync(_f, s); // TimeoutException, gdy outbox stoi

        ready.DeliveryId.Should().NotBeEmpty();
        (await MessagesAsync(poison)).Should().HaveCount(poison.Length).And.OnlyContain(m =>
            m.DeadLetteredAtUtc != null && m.ProcessedAtUtc == null && m.NextAttemptAtUtc == null
            && m.Attempts == 1 && m.Error != null);
    }

    [Fact]
    public async Task Permanent_errors_go_straight_to_dead_letter_and_are_never_picked_again()
    {
        var unknownAndBroken = await AddPoisonAsync(2, oldest: DateTime.UtcNow.AddYears(-1)); // nieznany typ + zły JSON
        var nullPayload = await AddMessageAsync(KnownTypeName<OrderReadyForPickup>(), "null", DateTime.UtcNow.AddYears(-1));
        var ids = unknownAndBroken.Append(nullPayload).ToArray();

        await DeliveryScenario.FlushOutboxAsync(_f);
        var dead = await MessagesAsync(ids);
        dead.Should().OnlyContain(m => m.DeadLetteredAtUtc != null && m.Attempts == 1 && m.NextAttemptAtUtc == null
            && m.ProcessedAtUtc == null && !string.IsNullOrEmpty(m.Error));
        dead.Should().OnlyContain(m => !m.Error!.Contains("{not json"), "błąd nie powiela ładunku");

        await DeliveryScenario.FlushOutboxAsync(_f);
        (await MessagesAsync(ids)).Should().OnlyContain(m => m.Attempts == 1, "odłożona wiadomość nie jest już pobierana");
    }

    [Fact]
    public async Task Transient_failure_waits_for_a_growing_backoff_and_is_never_dead_lettered()
    {
        var id = await AddProbeAsync(failures: int.MaxValue, oldest: DateTime.UtcNow.AddYears(-1));

        var before1 = DateTime.UtcNow;
        await DeliveryScenario.FlushOutboxAsync(_f);
        var first = await MessageAsync(id);
        (first.Attempts, first.DeadLetteredAtUtc).Should().Be((1, (DateTime?)null));
        first.NextAttemptAtUtc.Should().BeAfter(DateTime.UtcNow);

        await DeliveryScenario.FlushOutboxAsync(_f); // termin jeszcze nie minął — bez próby
        (await MessageAsync(id)).Attempts.Should().Be(1);

        await MakeDueAsync(id);
        var before2 = DateTime.UtcNow;
        await DeliveryScenario.FlushOutboxAsync(_f);
        var second = await MessageAsync(id);
        (second.Attempts, second.DeadLetteredAtUtc).Should().Be((2, (DateTime?)null));
        (second.NextAttemptAtUtc!.Value - before2).Should().BeGreaterThan(first.NextAttemptAtUtc!.Value - before1);

        // Po wielu próbach nadal bez odłożenia; odstęp ograniczony do 30 min.
        await MakeDueAsync(id, attempts: 50);
        var before3 = DateTime.UtcNow;
        await DeliveryScenario.FlushOutboxAsync(_f);
        var many = await MessageAsync(id);
        (many.Attempts, many.DeadLetteredAtUtc, many.ProcessedAtUtc).Should().Be((51, (DateTime?)null, (DateTime?)null));
        (many.NextAttemptAtUtc!.Value - before3).Should().BeCloseTo(MaxDelay, TimeSpan.FromSeconds(10));
    }

    [Fact]
    public async Task Transient_failure_recovers_and_the_event_is_delivered_exactly_once()
    {
        var id = await AddProbeAsync(failures: 2, oldest: DateTime.UtcNow.AddYears(-1));

        for (var i = 0; i < 3; i++)
        {
            await DeliveryScenario.FlushOutboxAsync(_f);
            await MakeDueAsync(id);
        }
        var done = await MessageAsync(id);
        (done.ProcessedAtUtc != null, done.Attempts, done.Error, done.NextAttemptAtUtc, done.DeadLetteredAtUtc)
            .Should().Be((true, 2, (string?)null, (DateTime?)null, (DateTime?)null));
        OutboxProbeHandler.Handled[ProbeEventId[id]].Should().Be(1);

        await DeliveryScenario.FlushOutboxAsync(_f);
        OutboxProbeHandler.Handled[ProbeEventId[id]].Should().Be(1, "wysłana wiadomość nie jest publikowana ponownie");
    }

    [Fact]
    public async Task A_full_batch_of_waiting_transient_failures_does_not_block_a_later_message()
    {
        var failing = new List<Guid>();
        for (var i = 0; i < BatchSize + 1; i++)
            failing.Add(await AddProbeAsync(failures: int.MaxValue, oldest: DateTime.UtcNow.AddYears(-2).AddSeconds(i)));
        var healthy = await AddProbeAsync(failures: 0, oldest: DateTime.UtcNow.AddYears(-2).AddMinutes(5));

        await DeliveryScenario.FlushOutboxAsync(_f); // pierwsza partia: same błędy → czekają na termin
        await DeliveryScenario.FlushOutboxAsync(_f); // druga: ostatnia błędna + zdrowa

        (await MessageAsync(healthy)).ProcessedAtUtc.Should().NotBeNull();
        OutboxProbeHandler.Handled[ProbeEventId[healthy]].Should().Be(1);
        (await MessagesAsync(failing.ToArray())).Should().OnlyContain(m =>
            m.Attempts == 1 && m.DeadLetteredAtUtc == null && m.NextAttemptAtUtc > DateTime.UtcNow);
    }

    // ---------- Pomocnicze ----------

    private static readonly ConcurrentDictionary<Guid, Guid> ProbeEventId = new(); // id wiadomości → id zdarzenia

    private string KnownTypeName<T>()
    {
        using var scope = _f.Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<IIntegrationEventTypeRegistry>().GetName(typeof(T));
    }

    /// <summary>Wiadomość zdarzenia testowego, którego handler zawiedzie <paramref name="failures"/> razy.</summary>
    private async Task<Guid> AddProbeAsync(int failures, DateTime oldest)
    {
        var e = new OutboxProbeEvent("probe");
        OutboxProbeHandler.FailuresLeft[e.Id] = failures;
        var messageId = await AddMessageAsync(KnownTypeName<OutboxProbeEvent>(), JsonSerializer.Serialize(e), oldest);
        ProbeEventId[messageId] = e.Id;
        return messageId;
    }

    /// <summary>Wiadomości w outboxie Ordering na przemian: nieznany typ i uszkodzony JSON znanego typu.</summary>
    private async Task<Guid[]> AddPoisonAsync(int count, DateTime oldest)
    {
        var known = KnownTypeName<OrderReadyForPickup>();
        var ids = new List<Guid>();
        for (var i = 0; i < count; i++)
            ids.Add(await AddMessageAsync(i % 2 == 0 ? "ZipZap.Tests.UnknownEvent" : known,
                i % 2 == 0 ? "{}" : "{not json", oldest.AddSeconds(i)));
        return ids.ToArray();
    }

    private async Task<Guid> AddMessageAsync(string type, string payload, DateTime occurredAtUtc)
    {
        using var scope = _f.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OrderingDbContext>();
        var m = new OutboxMessage { Type = type, Payload = payload, OccurredAtUtc = occurredAtUtc };
        db.OutboxMessages.Add(m);
        await db.SaveChangesAsync();
        return m.Id;
    }

    private async Task<List<OutboxMessage>> MessagesAsync(Guid[] ids)
    {
        using var scope = _f.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OrderingDbContext>();
        return await db.OutboxMessages.AsNoTracking().Where(m => ids.Contains(m.Id)).ToListAsync();
    }

    private async Task<OutboxMessage> MessageAsync(Guid id) => (await MessagesAsync(new[] { id })).Single();

    /// <summary>Termin kolejnej próby już minął (opcjonalnie: ustaw licznik prób).</summary>
    private async Task MakeDueAsync(Guid id, int? attempts = null)
    {
        using var scope = _f.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OrderingDbContext>();
        var m = await db.OutboxMessages.SingleAsync(x => x.Id == id);
        if (m.NextAttemptAtUtc != null) m.NextAttemptAtUtc = DateTime.UtcNow.AddSeconds(-1);
        if (attempts is int a) m.Attempts = a;
        await db.SaveChangesAsync();
    }
}
