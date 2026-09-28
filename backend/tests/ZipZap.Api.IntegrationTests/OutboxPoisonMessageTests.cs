using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using ZipZap.BuildingBlocks.Messaging;
using ZipZap.BuildingBlocks.Outbox;
using ZipZap.Contracts.Ordering;
using ZipZap.Modules.Ordering.Infrastructure;

namespace ZipZap.Api.IntegrationTests;

/// <summary>
/// Wiadomości „trujące" w outboxie (nieznany typ, uszkodzony ładunek) nie blokują kolejnych wiadomości modułu:
/// po błędzie czekają na backoff, a po wyczerpaniu prób są odkładane do martwych. Przetwarzanie ręczne
/// (dispatcher w tle wyłączony) — przez procesory WSZYSTKICH modułów, więc każda migracja outboxa jest użyta.
/// </summary>
[Collection("outbox")]
public sealed class OutboxPoisonMessageTests
{
    private const int BatchSize = OutboxProcessor<OrderingDbContext>.BatchSize;
    private const int MaxAttempts = OutboxProcessor<OrderingDbContext>.MaxAttempts;

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
        var rows = await MessagesAsync(poison);
        rows.Should().HaveCount(poison.Length).And.OnlyContain(m =>
            m.ProcessedAtUtc == null && m.DeadLetteredAtUtc == null && m.NextAttemptAtUtc != null
            && m.Attempts >= 1 && m.Attempts < MaxAttempts && m.Error != null);
    }

    [Fact]
    public async Task Failed_message_is_not_retried_before_its_backoff_and_the_backoff_grows()
    {
        var id = (await AddPoisonAsync(1, oldest: DateTime.UtcNow.AddYears(-1))).Single();

        var before1 = DateTime.UtcNow;
        await DeliveryScenario.FlushOutboxAsync(_f);
        var first = await MessageAsync(id);
        first.Attempts.Should().Be(1);
        first.NextAttemptAtUtc.Should().BeAfter(DateTime.UtcNow);

        await DeliveryScenario.FlushOutboxAsync(_f); // termin jeszcze nie minął — bez próby
        (await MessageAsync(id)).Attempts.Should().Be(1);

        await MakeDueAsync(id);
        var before2 = DateTime.UtcNow;
        await DeliveryScenario.FlushOutboxAsync(_f);
        var second = await MessageAsync(id);
        second.Attempts.Should().Be(2);
        second.DeadLetteredAtUtc.Should().BeNull();
        (second.NextAttemptAtUtc!.Value - before2).Should().BeGreaterThan(first.NextAttemptAtUtc!.Value - before1);
    }

    [Fact]
    public async Task Message_is_dead_lettered_after_max_attempts_and_never_picked_again()
    {
        var id = (await AddPoisonAsync(1, oldest: DateTime.UtcNow.AddYears(-1), attempts: MaxAttempts - 1)).Single();

        await DeliveryScenario.FlushOutboxAsync(_f);
        var dead = await MessageAsync(id);
        dead.Attempts.Should().Be(MaxAttempts);
        dead.DeadLetteredAtUtc.Should().NotBeNull();
        dead.NextAttemptAtUtc.Should().BeNull();
        dead.ProcessedAtUtc.Should().BeNull();
        dead.Error.Should().NotBeNullOrEmpty();

        await DeliveryScenario.FlushOutboxAsync(_f);
        (await MessageAsync(id)).Attempts.Should().Be(MaxAttempts);
    }

    /// <summary>Wiadomości w outboxie Ordering na przemian: nieznany typ i uszkodzony JSON znanego typu.</summary>
    private async Task<Guid[]> AddPoisonAsync(int count, DateTime oldest, int attempts = 0)
    {
        using var scope = _f.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OrderingDbContext>();
        var knownType = scope.ServiceProvider.GetRequiredService<IIntegrationEventTypeRegistry>()
            .GetName(typeof(OrderReadyForPickup));
        var messages = Enumerable.Range(0, count).Select(i => new OutboxMessage
        {
            Type = i % 2 == 0 ? "ZipZap.Tests.UnknownEvent" : knownType,
            Payload = i % 2 == 0 ? "{}" : "{not json",
            OccurredAtUtc = oldest.AddSeconds(i),
            Attempts = attempts,
        }).ToList();
        db.OutboxMessages.AddRange(messages);
        await db.SaveChangesAsync();
        return messages.Select(m => m.Id).ToArray();
    }

    private async Task<List<OutboxMessage>> MessagesAsync(Guid[] ids)
    {
        using var scope = _f.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OrderingDbContext>();
        return await db.OutboxMessages.AsNoTracking().Where(m => ids.Contains(m.Id)).ToListAsync();
    }

    private async Task<OutboxMessage> MessageAsync(Guid id) => (await MessagesAsync(new[] { id })).Single();

    private async Task MakeDueAsync(Guid id)
    {
        using var scope = _f.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OrderingDbContext>();
        await db.OutboxMessages.Where(m => m.Id == id)
            .ExecuteUpdateAsync(u => u.SetProperty(m => m.NextAttemptAtUtc, DateTime.UtcNow.AddSeconds(-1)));
    }
}
