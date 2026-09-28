using System.Collections.Concurrent;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;
using ZipZap.BuildingBlocks.Messaging;
using ZipZap.BuildingBlocks.Outbox;
using ZipZap.Contracts.Ordering;
using ZipZap.Modules.Ordering.Infrastructure;

namespace ZipZap.Api.IntegrationTests;

/// <summary>Zdarzenie testowe outboxa (rejestrowane tylko w <see cref="OutboxApiFactory"/>).</summary>
public sealed record OutboxProbeEvent(string Tag) : IntegrationEvent;

/// <summary>
/// Handler testowy: dla danego zdarzenia zawodzi zadaną liczbę razy (przejściowa awaria), potem działa.
/// Treść wyjątku celowo zawiera dane osobowe — nie może trafić do bazy (pole Error) ani do panelu.
/// </summary>
public sealed class OutboxProbeHandler : IIntegrationEventHandler<OutboxProbeEvent>
{
    public const string SecretInExceptionMessage = "jan.kowalski@example.com";
    public const string TokenInExceptionMessage = "SEKRET-TESTOWY-123";

    public static readonly ConcurrentDictionary<Guid, int> FailuresLeft = new();
    public static readonly ConcurrentDictionary<Guid, int> Handled = new();
    public static readonly ConcurrentDictionary<Guid, int> Running = new();
    public static readonly ConcurrentDictionary<Guid, int> MaxConcurrent = new();
    public static readonly ConcurrentDictionary<Guid, int> DelayMs = new();

    /// <summary>Bramka: obsługa zdarzenia sygnalizuje wejście i czeka, aż test ją zwolni (wymuszenie kolejności).</summary>
    public static readonly ConcurrentDictionary<Guid, (TaskCompletionSource Entered, TaskCompletionSource Release)> Gates = new();

    public async Task HandleAsync(OutboxProbeEvent e, CancellationToken ct = default)
    {
        var now = Running.AddOrUpdate(e.Id, 1, (_, n) => n + 1);
        MaxConcurrent.AddOrUpdate(e.Id, now, (_, n) => Math.Max(n, now));
        try
        {
            if (Gates.TryGetValue(e.Id, out var gate))
            {
                gate.Entered.TrySetResult();
                await gate.Release.Task.WaitAsync(ct);
            }
            if (DelayMs.TryGetValue(e.Id, out var delay)) await Task.Delay(delay, ct);
            if (FailuresLeft.TryGetValue(e.Id, out var left) && left > 0)
            {
                FailuresLeft[e.Id] = left - 1;
                throw new InvalidOperationException(
                    $"Symulowana awaria handlera dla {SecretInExceptionMessage}, token={TokenInExceptionMessage}.");
            }
            Handled.AddOrUpdate(e.Id, 1, (_, n) => n + 1);
        }
        finally
        {
            Running.AddOrUpdate(e.Id, 0, (_, n) => n - 1);
        }
    }
}

/// <summary>Drugi handler tego samego zdarzenia (zarejestrowany PRZED zawodnym) — zawsze działa, liczy wywołania.</summary>
public sealed class OutboxProbeCompanionHandler : IIntegrationEventHandler<OutboxProbeEvent>
{
    public static readonly ConcurrentDictionary<Guid, int> Handled = new();

    public Task HandleAsync(OutboxProbeEvent e, CancellationToken ct = default)
    {
        Handled.AddOrUpdate(e.Id, 1, (_, n) => n + 1);
        return Task.CompletedTask;
    }
}

/// <summary>
/// Outbox: błędy TRWAŁE (nieznany typ, nieczytelny ładunek) → od razu do martwych; błędy PRZEJŚCIOWE → ponawiane
/// bez limitu z narastającym odstępem (maks. 30 min), nigdy odkładane. Żadna nieudana wiadomość nie blokuje kolejnych.
/// Kilka instancji nie wysyła tej samej wiadomości równolegle (dzierżawa), a powtórka nie dubluje skutków handlera,
/// który już zdarzenie obsłużył. W polu Error tylko kod kategorii — nigdy treść wyjątku.
/// Przetwarzanie ręczne (dispatcher w tle wyłączony) — przez procesory WSZYSTKICH modułów.
/// </summary>
[Collection("outbox")]
public sealed class OutboxPoisonMessageTests
{
    private const int BatchSize = OutboxProcessor<OrderingDbContext>.BatchSize;
    private static readonly TimeSpan MaxDelay = OutboxProcessor<OrderingDbContext>.MaxRetryDelay;
    private static readonly string[] PermanentCategories =
        { OutboxErrorCategory.UnknownType, OutboxErrorCategory.UnreadablePayload };

    private readonly OutboxApiFactory _f;
    public OutboxPoisonMessageTests(OutboxApiFactory f) => _f = f;

    [Fact]
    public async Task Poison_messages_filling_a_whole_batch_do_not_block_a_later_OrderReadyForPickup()
    {
        var s = await OrderScenario.StoreWithSlotAsync(_f);
        // Starsze niż wszystko, co wygeneruje zamówienie, i więcej niż jedna partia — dawniej pierwsza partia
        // na zawsze składała się z samych trujących wiadomości, a dostawa nigdy nie powstawała.
        var poison = await OutboxTestData.AddPoisonAsync(_f, BatchSize + 5, oldest: DateTime.UtcNow.AddDays(-1));

        var ready = await DeliveryScenario.ReadyOrderAsync(_f, s); // TimeoutException, gdy outbox stoi

        ready.DeliveryId.Should().NotBeEmpty();
        (await OutboxTestData.MessagesAsync(_f, poison)).Should().HaveCount(poison.Length).And.OnlyContain(m =>
            m.DeadLetteredAtUtc != null && m.ProcessedAtUtc == null && m.NextAttemptAtUtc == null
            && m.LockedUntilUtc == null && m.Attempts == 1 && PermanentCategories.Contains(m.Error));
    }

    [Fact]
    public async Task Permanent_errors_go_straight_to_dead_letter_with_a_category_and_are_never_picked_again()
    {
        var old = DateTime.UtcNow.AddYears(-1);
        var unknown = await OutboxTestData.AddMessageAsync(_f, "ZipZap.Tests.UnknownEvent", "{}", old);
        var broken = await OutboxTestData.AddMessageAsync(_f, OutboxTestData.KnownTypeName<OrderReadyForPickup>(_f), "{not json", old);
        var nullPayload = await OutboxTestData.AddMessageAsync(_f, OutboxTestData.KnownTypeName<OrderReadyForPickup>(_f), "null", old);
        var ids = new[] { unknown, broken, nullPayload };

        await DeliveryScenario.FlushOutboxAsync(_f);
        var dead = (await OutboxTestData.MessagesAsync(_f, ids)).ToDictionary(m => m.Id);
        dead.Values.Should().OnlyContain(m => m.DeadLetteredAtUtc != null && m.Attempts == 1 && m.NextAttemptAtUtc == null
            && m.ProcessedAtUtc == null && m.LockedUntilUtc == null);
        dead[unknown].Error.Should().Be(OutboxErrorCategory.UnknownType, "tylko kod kategorii, bez nazwy typu i ładunku");
        dead[broken].Error.Should().Be(OutboxErrorCategory.UnreadablePayload);
        dead[nullPayload].Error.Should().Be(OutboxErrorCategory.UnreadablePayload);

        await DeliveryScenario.FlushOutboxAsync(_f);
        (await OutboxTestData.MessagesAsync(_f, ids)).Should().OnlyContain(m => m.Attempts == 1,
            "odłożona wiadomość nie jest już pobierana");
    }

    [Fact]
    public async Task Transient_failure_waits_for_a_growing_backoff_and_is_never_dead_lettered()
    {
        var id = await OutboxTestData.AddProbeAsync(_f, failures: int.MaxValue, oldest: DateTime.UtcNow.AddYears(-1));

        var before1 = DateTime.UtcNow;
        await DeliveryScenario.FlushOutboxAsync(_f);
        var first = await OutboxTestData.MessageAsync(_f, id);
        (first.Attempts, first.DeadLetteredAtUtc, first.LockedUntilUtc).Should().Be((1, (DateTime?)null, (DateTime?)null));
        first.NextAttemptAtUtc.Should().BeAfter(DateTime.UtcNow);
        // Pole Error: kategoria, nigdy treść wyjątku (a ta zawiera tu adres e-mail).
        first.Error.Should().Be(OutboxErrorCategory.HandlerError);

        await DeliveryScenario.FlushOutboxAsync(_f); // termin jeszcze nie minął — bez próby
        (await OutboxTestData.MessageAsync(_f, id)).Attempts.Should().Be(1);

        await OutboxTestData.MakeDueAsync(_f, id);
        var before2 = DateTime.UtcNow;
        await DeliveryScenario.FlushOutboxAsync(_f);
        var second = await OutboxTestData.MessageAsync(_f, id);
        (second.Attempts, second.DeadLetteredAtUtc).Should().Be((2, (DateTime?)null));
        (second.NextAttemptAtUtc!.Value - before2).Should().BeGreaterThan(first.NextAttemptAtUtc!.Value - before1);

        // Po wielu próbach nadal bez odłożenia; odstęp ograniczony do 30 min.
        await OutboxTestData.MakeDueAsync(_f, id, attempts: 50);
        var before3 = DateTime.UtcNow;
        await DeliveryScenario.FlushOutboxAsync(_f);
        var many = await OutboxTestData.MessageAsync(_f, id);
        (many.Attempts, many.DeadLetteredAtUtc, many.ProcessedAtUtc).Should().Be((51, (DateTime?)null, (DateTime?)null));
        (many.NextAttemptAtUtc!.Value - before3).Should().BeCloseTo(MaxDelay, TimeSpan.FromSeconds(10));
        many.Error.Should().NotContain(OutboxProbeHandler.SecretInExceptionMessage);
    }

    [Fact]
    public async Task Transient_failure_recovers_and_the_event_is_delivered_exactly_once()
    {
        var id = await OutboxTestData.AddProbeAsync(_f, failures: 2, oldest: DateTime.UtcNow.AddYears(-1));

        for (var i = 0; i < 3; i++)
        {
            await DeliveryScenario.FlushOutboxAsync(_f);
            await OutboxTestData.MakeDueAsync(_f, id);
        }
        var done = await OutboxTestData.MessageAsync(_f, id);
        (done.ProcessedAtUtc != null, done.Attempts, done.Error, done.NextAttemptAtUtc, done.DeadLetteredAtUtc)
            .Should().Be((true, 2, (string?)null, (DateTime?)null, (DateTime?)null));
        OutboxProbeHandler.Handled[OutboxTestData.EventId(id)].Should().Be(1);

        await DeliveryScenario.FlushOutboxAsync(_f);
        OutboxProbeHandler.Handled[OutboxTestData.EventId(id)].Should().Be(1, "wysłana wiadomość nie jest publikowana ponownie");
    }

    [Fact]
    public async Task A_full_batch_of_waiting_transient_failures_does_not_block_a_later_message()
    {
        var failing = new List<Guid>();
        for (var i = 0; i < BatchSize + 1; i++)
            failing.Add(await OutboxTestData.AddProbeAsync(_f, failures: int.MaxValue, oldest: DateTime.UtcNow.AddYears(-2).AddSeconds(i)));
        var healthy = await OutboxTestData.AddProbeAsync(_f, failures: 0, oldest: DateTime.UtcNow.AddYears(-2).AddMinutes(5));

        await DeliveryScenario.FlushOutboxAsync(_f); // pierwsza partia: same błędy → czekają na termin
        await DeliveryScenario.FlushOutboxAsync(_f); // druga: ostatnia błędna + zdrowa

        (await OutboxTestData.MessageAsync(_f, healthy)).ProcessedAtUtc.Should().NotBeNull();
        OutboxProbeHandler.Handled[OutboxTestData.EventId(healthy)].Should().Be(1);
        (await OutboxTestData.MessagesAsync(_f, failing.ToArray())).Should().OnlyContain(m =>
            m.Attempts == 1 && m.DeadLetteredAtUtc == null && m.NextAttemptAtUtc > DateTime.UtcNow);
    }

    // ---------- Wiele instancji i powtórki ----------

    [Fact]
    public async Task Two_instances_processing_at_the_same_time_publish_each_message_only_once()
    {
        var ids = new List<Guid>();
        for (var i = 0; i < BatchSize + 10; i++)
        {
            var id = await OutboxTestData.AddProbeAsync(_f, failures: 0, oldest: DateTime.UtcNow.AddYears(-3).AddSeconds(i));
            OutboxProbeHandler.DelayMs[OutboxTestData.EventId(id)] = 30; // obsługa trwa — instancje na pewno się nakładają
            ids.Add(id);
        }

        // Dwie „instancje API" = dwa niezależne procesory Ordering z osobnymi połączeniami, start w tej samej chwili.
        using var go = new SemaphoreSlim(0);
        var instances = Enumerable.Range(0, 2).Select(async _ =>
        {
            await go.WaitAsync();
            for (var round = 0; round < 5; round++)
            {
                using var scope = _f.Services.CreateScope();
                await scope.ServiceProvider.GetServices<IOutboxProcessor>().OfType<OutboxProcessor<OrderingDbContext>>()
                    .Single().ProcessPendingAsync();
            }
        }).ToList();
        go.Release(2);
        await Task.WhenAll(instances);

        (await OutboxTestData.MessagesAsync(_f, ids.ToArray())).Should().OnlyContain(m =>
            m.ProcessedAtUtc != null && m.LockedUntilUtc == null && m.Attempts == 0);
        ids.Select(OutboxTestData.EventId).Should().OnlyContain(e =>
            OutboxProbeHandler.Handled[e] == 1 && OutboxProbeHandler.MaxConcurrent[e] == 1,
            "każda wiadomość obsłużona dokładnie raz i nigdy równolegle przez dwie instancje");
    }

    [Fact]
    public async Task A_row_being_claimed_by_another_instance_is_skipped_instead_of_blocking_or_duplicating()
    {
        var busy = await OutboxTestData.AddProbeAsync(_f, failures: 0, oldest: DateTime.UtcNow.AddYears(-6));
        var free = await OutboxTestData.AddProbeAsync(_f, failures: 0, oldest: DateTime.UtcNow.AddYears(-6).AddSeconds(1));

        // Inna instancja jest dokładnie w trakcie pobierania tej wiadomości (trzyma blokadę wiersza, jeszcze bez commitu).
        using var scope = _f.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OrderingDbContext>();
        await using var other = new NpgsqlConnection(db.Database.GetConnectionString());
        await other.OpenAsync();
        await using var tx = await other.BeginTransactionAsync();
        await using (var lockRow = new NpgsqlCommand(
            "SELECT 1 FROM ordering.outbox_messages WHERE \"Id\" = @id FOR UPDATE", other, tx))
        {
            lockRow.Parameters.AddWithValue("id", busy);
            await lockRow.ExecuteScalarAsync();
        }

        var flush = DeliveryScenario.FlushOutboxAsync(_f);
        (await Task.WhenAny(flush, Task.Delay(TimeSpan.FromSeconds(10)))).Should().BeSameAs(flush,
            "zablokowany wiersz jest pomijany (SKIP LOCKED), a nie blokuje procesora");
        await flush;
        (await OutboxTestData.MessageAsync(_f, free)).ProcessedAtUtc.Should().NotBeNull();
        (await OutboxTestData.MessageAsync(_f, busy)).ProcessedAtUtc.Should().BeNull("tę wysyła druga instancja");
        OutboxProbeHandler.Handled.ContainsKey(OutboxTestData.EventId(busy)).Should().BeFalse();

        await tx.RollbackAsync(); // druga instancja „padła" przed zapisem dzierżawy — wiadomość nie ginie
        await DeliveryScenario.FlushOutboxAsync(_f);
        OutboxProbeHandler.Handled[OutboxTestData.EventId(busy)].Should().Be(1);
    }

    [Fact]
    public async Task A_message_held_by_another_instance_waits_and_returns_after_its_lease_expires()
    {
        var id = await OutboxTestData.AddProbeAsync(_f, failures: 0, oldest: DateTime.UtcNow.AddYears(-3));
        await OutboxTestData.SetLockAsync(_f, id, DateTime.UtcNow.AddMinutes(4)); // inna instancja właśnie ją wysyła

        await DeliveryScenario.FlushOutboxAsync(_f);
        (await OutboxTestData.MessageAsync(_f, id)).ProcessedAtUtc.Should().BeNull("dzierżawa innej instancji jest respektowana");
        OutboxProbeHandler.Handled.ContainsKey(OutboxTestData.EventId(id)).Should().BeFalse();

        await OutboxTestData.SetLockAsync(_f, id, DateTime.UtcNow.AddSeconds(-1)); // instancja padła — dzierżawa wygasła
        await DeliveryScenario.FlushOutboxAsync(_f);
        var done = await OutboxTestData.MessageAsync(_f, id);
        (done.ProcessedAtUtc != null, done.LockedUntilUtc, done.Attempts).Should().Be((true, (DateTime?)null, 0));
        OutboxProbeHandler.Handled[OutboxTestData.EventId(id)].Should().Be(1, "żadne zdarzenie nie ginie po awarii instancji");
    }

    [Fact]
    public async Task Retry_after_one_handler_failed_does_not_repeat_handlers_that_already_succeeded()
    {
        var id = await OutboxTestData.AddProbeAsync(_f, failures: 1, oldest: DateTime.UtcNow.AddYears(-3));
        var eventId = OutboxTestData.EventId(id);

        await DeliveryScenario.FlushOutboxAsync(_f); // towarzyszący OK, zawodny rzuca → ponowienie całego zdarzenia
        OutboxProbeCompanionHandler.Handled[eventId].Should().Be(1);
        (await OutboxTestData.MessageAsync(_f, id)).Attempts.Should().Be(1);

        await OutboxTestData.MakeDueAsync(_f, id);
        await DeliveryScenario.FlushOutboxAsync(_f);

        (await OutboxTestData.MessageAsync(_f, id)).ProcessedAtUtc.Should().NotBeNull();
        OutboxProbeHandler.Handled[eventId].Should().Be(1);
        OutboxProbeCompanionHandler.Handled[eventId].Should().Be(1, "handler, który już obsłużył zdarzenie, jest pomijany");
    }
}

/// <summary>Wspólne dane testowe outboxa (moduł Ordering) — używane też przez testy panelu.</summary>
internal static class OutboxTestData
{
    private static readonly ConcurrentDictionary<Guid, Guid> ProbeEventIds = new(); // id wiadomości → id zdarzenia

    public static Guid EventId(Guid messageId) => ProbeEventIds[messageId];

    public static string KnownTypeName<T>(ApiFactory f)
    {
        using var scope = f.Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<IIntegrationEventTypeRegistry>().GetName(typeof(T));
    }

    /// <summary>Wiadomość zdarzenia testowego, którego handler zawiedzie <paramref name="failures"/> razy.</summary>
    public static async Task<Guid> AddProbeAsync(ApiFactory f, int failures, DateTime oldest, string tag = "probe",
        Action<OutboxMessage>? tweak = null)
    {
        var e = new OutboxProbeEvent(tag);
        OutboxProbeHandler.FailuresLeft[e.Id] = failures;
        var messageId = await AddMessageAsync(f, KnownTypeName<OutboxProbeEvent>(f), JsonSerializer.Serialize(e), oldest, tweak);
        ProbeEventIds[messageId] = e.Id;
        return messageId;
    }

    /// <summary>Wiadomości w outboxie Ordering na przemian: nieznany typ i uszkodzony JSON znanego typu.</summary>
    public static async Task<Guid[]> AddPoisonAsync(ApiFactory f, int count, DateTime oldest)
    {
        var known = KnownTypeName<OrderReadyForPickup>(f);
        var ids = new List<Guid>();
        for (var i = 0; i < count; i++)
            ids.Add(await AddMessageAsync(f, i % 2 == 0 ? "ZipZap.Tests.UnknownEvent" : known,
                i % 2 == 0 ? "{}" : "{not json", oldest.AddSeconds(i)));
        return ids.ToArray();
    }

    public static Task<Guid> AddMessageAsync(ApiFactory f, string type, string payload, DateTime occurredAtUtc,
        Action<OutboxMessage>? tweak = null)
        => AddMessageAsync<OrderingDbContext>(f, type, payload, occurredAtUtc, tweak);

    /// <summary>Wiadomość w outboxie wskazanego modułu.</summary>
    public static async Task<Guid> AddMessageAsync<TContext>(ApiFactory f, string type, string payload, DateTime occurredAtUtc,
        Action<OutboxMessage>? tweak = null) where TContext : DbContext, IOutboxDbContext
    {
        using var scope = f.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TContext>();
        var m = new OutboxMessage { Type = type, Payload = payload, OccurredAtUtc = occurredAtUtc };
        tweak?.Invoke(m);
        db.OutboxMessages.Add(m);
        await db.SaveChangesAsync();
        return m.Id;
    }

    /// <summary>Zdarzenie jako wiadomość outboxa modułu (Id wiadomości = Id zdarzenia, jak w produkcji).</summary>
    public static Task<Guid> AddEventAsync<TContext>(ApiFactory f, IIntegrationEvent e) where TContext : DbContext, IOutboxDbContext
    {
        using var scope = f.Services.CreateScope();
        var registry = scope.ServiceProvider.GetRequiredService<IIntegrationEventTypeRegistry>();
        return AddMessageAsync<TContext>(f, registry.GetName(e.GetType()), JsonSerializer.Serialize(e, e.GetType()),
            e.OccurredAtUtc, m => m.Id = e.Id);
    }

    /// <summary>Zdarzenie testowe w outboxie wskazanego modułu (np. Catalog — ta sama partia co zdarzenia Catalog).</summary>
    public static async Task<Guid> AddProbeAsync<TContext>(ApiFactory f, DateTime oldest) where TContext : DbContext, IOutboxDbContext
    {
        var e = new OutboxProbeEvent("probe") { OccurredAtUtc = oldest };
        var messageId = await AddEventAsync<TContext>(f, e);
        ProbeEventIds[messageId] = e.Id;
        return messageId;
    }

    public static async Task<List<OutboxMessage>> MessagesAsync<TContext>(ApiFactory f, params Guid[] ids)
        where TContext : DbContext, IOutboxDbContext
    {
        using var scope = f.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TContext>();
        return await db.OutboxMessages.AsNoTracking().Where(m => ids.Contains(m.Id)).ToListAsync();
    }

    public static async Task<List<OutboxMessage>> MessagesAsync(ApiFactory f, Guid[] ids)
    {
        using var scope = f.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OrderingDbContext>();
        return await db.OutboxMessages.AsNoTracking().Where(m => ids.Contains(m.Id)).ToListAsync();
    }

    public static async Task<OutboxMessage> MessageAsync(ApiFactory f, Guid id) => (await MessagesAsync(f, new[] { id })).Single();

    /// <summary>Termin kolejnej próby już minął (opcjonalnie: ustaw licznik prób).</summary>
    public static async Task MakeDueAsync(ApiFactory f, Guid id, int? attempts = null)
    {
        using var scope = f.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OrderingDbContext>();
        var m = await db.OutboxMessages.SingleAsync(x => x.Id == id);
        if (m.NextAttemptAtUtc != null) m.NextAttemptAtUtc = DateTime.UtcNow.AddSeconds(-1);
        if (attempts is int a) m.Attempts = a;
        await db.SaveChangesAsync();
    }

    /// <summary>Symuluje dzierżawę innej instancji API (w przyszłości = trzyma; w przeszłości = wygasła).</summary>
    public static async Task SetLockAsync(ApiFactory f, Guid id, DateTime lockedUntilUtc)
    {
        using var scope = f.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OrderingDbContext>();
        var m = await db.OutboxMessages.SingleAsync(x => x.Id == id);
        m.LockedUntilUtc = lockedUntilUtc;
        await db.SaveChangesAsync();
    }
}
