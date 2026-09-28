using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using ZipZap.BuildingBlocks.Outbox;
using ZipZap.Modules.Audit.Infrastructure;

namespace ZipZap.Api.IntegrationTests;

/// <summary>
/// Panel odłożonych zdarzeń outboxa: tylko administrator, tylko bezpieczne metadane (bez ładunku, bez treści
/// błędów, bez danych klientów), ręczne ponowienie JEDNEGO zdarzenia — warunkowe (podwójne kliknięcie / równoległe
/// żądania ponawiają dokładnie raz) i audytowane (autor + czas).
/// </summary>
[Collection("outbox")]
public sealed class OutboxAdminPanelTests
{
    private const string ListUrl = "/api/admin/outbox";

    // Dane osobowe w ładunku i w starej (sprzed kategoryzacji) treści błędu — nie mogą wyjść w odpowiedzi.
    private const string Email = "anna.nowak.pii@example.com";
    private const string Phone = "600987654";
    private const string Street = "ul. Sekretna 13";
    private const string LegacyRawError = "SMTP 550 mailbox anna.nowak.pii@example.com rejected";

    private static readonly string[] AllowedItemFields =
    {
        "module", "id", "type", "occurredAtUtc", "deadLetteredAtUtc", "nextAttemptAtUtc", "attempts",
        "errorCategory", "errorDescription", "lastManualRetryAtUtc", "lastManualRetryByUserId",
    };

    private readonly OutboxApiFactory _f;
    public OutboxAdminPanelTests(OutboxApiFactory f) => _f = f;

    [Fact]
    public async Task Only_the_platform_admin_can_list_or_retry()
    {
        var id = await AddDeadProbeAsync();
        var admin = await _f.LoginAdminAsync();
        var storeId = await _f.CreateStoreAsync(admin.accessToken);
        var others = new[]
        {
            (await _f.RegisterCustomerAsync()).accessToken,
            (await _f.CreateEmployeeAsync(admin.accessToken, storeId)).accessToken,
            (await _f.CreateStaffAsync(admin.accessToken, storeId, "Driver")).accessToken,
        };

        (await _f.Anon().GetAsync(ListUrl)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await RetryAsync(_f.Anon(), id, 1)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        foreach (var token in others)
        {
            (await _f.Authed(token).GetAsync(ListUrl)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
            (await RetryAsync(_f.Authed(token), id, 1)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }

        var untouched = await OutboxTestData.MessageAsync(_f, id);
        (untouched.DeadLetteredAtUtc != null, untouched.LastManualRetryAtUtc, untouched.LastManualRetryByUserId)
            .Should().Be((true, (DateTime?)null, (Guid?)null), "odmowa nie zmienia zdarzenia");
        (await AuditCountAsync(id)).Should().Be(0);
        (await _f.Authed(admin.accessToken).GetAsync(ListUrl)).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task List_returns_only_safe_metadata_without_payload_error_text_or_customer_data()
    {
        var piiPayload = JsonSerializer.Serialize(new { email = Email, phone = Phone, address = Street });
        var dead = await OutboxTestData.AddMessageAsync(_f, "ZipZap.Tests.RemovedEvent", piiPayload, DateTime.UtcNow.AddDays(-2),
            m => { m.Attempts = 1; m.DeadLetteredAtUtc = DateTime.UtcNow.AddDays(-1); m.Error = LegacyRawError; });
        var longRetry = await OutboxTestData.AddProbeAsync(_f, failures: int.MaxValue, oldest: DateTime.UtcNow.AddDays(-2),
            tag: $"{Email} {Phone} {Street}", tweak: m =>
            {
                m.Attempts = OutboxPolicy.LongRetryAttempts + 2;
                m.NextAttemptAtUtc = DateTime.UtcNow.AddMinutes(25);
                m.Error = OutboxErrorCategory.ExternalService;
            });
        var shortRetry = await OutboxTestData.AddProbeAsync(_f, failures: int.MaxValue, oldest: DateTime.UtcNow.AddDays(-2),
            tweak: m => { m.Attempts = 2; m.NextAttemptAtUtc = DateTime.UtcNow.AddMinutes(1); m.Error = OutboxErrorCategory.Database; });

        var admin = await _f.LoginAdminAsync();
        var resp = await _f.Authed(admin.accessToken).GetAsync(ListUrl);
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var raw = await resp.Content.ReadAsStringAsync();

        foreach (var secret in new[] { Email, Phone, Street, "SMTP", "Sekretna", "payload", "Payload", "\"tag\"" })
            raw.Should().NotContain(secret, "odpowiedź panelu nie może zawierać ładunku, treści błędów ani danych klientów");

        using var doc = JsonDocument.Parse(raw);
        var root = doc.RootElement;
        var deadItems = root.GetProperty("deadLettered").EnumerateArray().ToList();
        var longItems = root.GetProperty("longRetrying").EnumerateArray().ToList();
        deadItems.Concat(longItems).Should().OnlyContain(i =>
            i.EnumerateObject().Select(p => p.Name).All(n => AllowedItemFields.Contains(n)), "tylko pola z białej listy");

        var d = deadItems.Single(i => i.GetProperty("id").GetGuid() == dead);
        d.GetProperty("module").GetString().Should().Be("ordering");
        d.GetProperty("type").GetString().Should().Be("ZipZap.Tests.RemovedEvent");
        d.GetProperty("attempts").GetInt32().Should().Be(1);
        d.GetProperty("errorCategory").GetString().Should().Be(OutboxErrorCategory.Legacy, "stara treść błędu jest ukryta");
        d.GetProperty("errorDescription").GetString().Should().NotBeNullOrWhiteSpace();
        d.GetProperty("deadLetteredAtUtc").ValueKind.Should().Be(JsonValueKind.String);
        d.GetProperty("occurredAtUtc").ValueKind.Should().Be(JsonValueKind.String);

        // Długo ponawiane: widoczne w osobnej liście, ale NIE jako odłożone (reguła bez limitu prób bez zmian).
        var l = longItems.Single(i => i.GetProperty("id").GetGuid() == longRetry);
        l.GetProperty("errorCategory").GetString().Should().Be(OutboxErrorCategory.ExternalService);
        l.GetProperty("deadLetteredAtUtc").ValueKind.Should().Be(JsonValueKind.Null);
        deadItems.Should().NotContain(i => i.GetProperty("id").GetGuid() == longRetry);
        longItems.Should().NotContain(i => i.GetProperty("id").GetGuid() == shortRetry, "2 próby to jeszcze nie „długo”");
        (await OutboxTestData.MessageAsync(_f, longRetry)).DeadLetteredAtUtc.Should().BeNull();

        var summary = root.GetProperty("summary");
        summary.GetProperty("deadLettered").GetInt32().Should().BeGreaterThanOrEqualTo(1);
        summary.GetProperty("retrying").GetInt32().Should().BeGreaterThanOrEqualTo(2);
        summary.GetProperty("longRetrying").GetInt32().Should().BeGreaterThanOrEqualTo(1);
        root.GetProperty("longRetryAttempts").GetInt32().Should().Be(OutboxPolicy.LongRetryAttempts);
        root.GetProperty("maxRetryDelayMinutes").GetInt32().Should().Be(30);
    }

    [Fact]
    public async Task Admin_retry_requeues_the_event_once_it_is_delivered_exactly_once_and_the_author_is_audited()
    {
        // Odłożona po błędzie trwałym; przyczynę usunięto (typ zdarzenia jest znów znany) — administrator ponawia.
        var id = await AddDeadProbeAsync();
        var admin = await _f.LoginAdminAsync();
        var adminId = Guid.Parse(admin.user.id);
        var before = DateTime.UtcNow.AddSeconds(-1);

        var resp = await RetryAsync(_f.Authed(admin.accessToken), id, expectedAttempts: 1);
        resp.StatusCode.Should().Be(HttpStatusCode.OK);

        var requeued = await OutboxTestData.MessageAsync(_f, id);
        (requeued.DeadLetteredAtUtc, requeued.NextAttemptAtUtc, requeued.LockedUntilUtc, requeued.ProcessedAtUtc)
            .Should().Be(((DateTime?)null, (DateTime?)null, (DateTime?)null, (DateTime?)null));
        requeued.LastManualRetryByUserId.Should().Be(adminId);
        requeued.LastManualRetryAtUtc.Should().BeAfter(before);

        var audit = await AuditEntriesAsync(id);
        audit.Should().ContainSingle();
        (audit[0].ActorUserId, audit[0].EntityType).Should().Be(((Guid?)adminId, "outbox_message"));
        audit[0].CreatedAtUtc.Should().BeAfter(before);
        audit[0].Details.Should().NotContain("tag").And.NotContain("probe", "w audycie też bez ładunku");

        await DeliveryScenario.FlushOutboxAsync(_f);
        (await OutboxTestData.MessageAsync(_f, id)).ProcessedAtUtc.Should().NotBeNull();
        OutboxProbeHandler.Handled[OutboxTestData.EventId(id)].Should().Be(1);
    }

    [Fact]
    public async Task Double_click_and_parallel_retries_requeue_only_once()
    {
        var admin = await _f.LoginAdminAsync();

        // Podwójne kliknięcie: drugie żądanie z tym samym stanem → 409, bez drugiego wpisu audytu.
        var clicked = await AddDeadProbeAsync();
        (await RetryAsync(_f.Authed(admin.accessToken), clicked, 1)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await RetryAsync(_f.Authed(admin.accessToken), clicked, 1)).StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await AuditCountAsync(clicked)).Should().Be(1);

        // Równoległe żądania (np. dwóch administratorów naraz): wygrywa dokładnie jedno.
        var raced = await AddDeadProbeAsync();
        var responses = await OrderScenario.FireTogetherAsync(Enumerable.Range(0, 4)
            .Select(_ => (Func<Task<HttpResponseMessage>>)(() => RetryAsync(_f.Authed(admin.accessToken), raced, 1)))
            .ToList());
        responses.Count(r => r.StatusCode == HttpStatusCode.OK).Should().Be(1);
        responses.Count(r => r.StatusCode == HttpStatusCode.Conflict).Should().Be(3);
        (await AuditCountAsync(raced)).Should().Be(1);

        // Spóźnione kliknięcie po tym, jak zdarzenie znowu zostało odłożone (inna liczba prób) → 409, bez ponowienia.
        var stale = await OutboxTestData.AddMessageAsync(_f, "ZipZap.Tests.StillUnknownEvent", "{}", DateTime.UtcNow.AddYears(-4),
            m => { m.Attempts = 1; m.DeadLetteredAtUtc = DateTime.UtcNow; m.Error = OutboxErrorCategory.UnknownType; });
        (await RetryAsync(_f.Authed(admin.accessToken), stale, 1)).StatusCode.Should().Be(HttpStatusCode.OK);
        await DeliveryScenario.FlushOutboxAsync(_f); // typ nadal nieznany → znów odłożone, próba nr 2
        (await OutboxTestData.MessageAsync(_f, stale)).Should().Match<OutboxMessage>(m => m.DeadLetteredAtUtc != null && m.Attempts == 2);
        (await RetryAsync(_f.Authed(admin.accessToken), stale, 1)).StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await OutboxTestData.MessageAsync(_f, stale)).DeadLetteredAtUtc.Should().NotBeNull();
    }

    [Fact]
    public async Task Retry_is_refused_for_unknown_or_not_dead_lettered_events()
    {
        var admin = _f.Authed((await _f.LoginAdminAsync()).accessToken);
        var pending = await OutboxTestData.AddProbeAsync(_f, failures: int.MaxValue, oldest: DateTime.UtcNow,
            tweak: m => { m.Attempts = 3; m.NextAttemptAtUtc = DateTime.UtcNow.AddMinutes(5); });
        var processed = await OutboxTestData.AddProbeAsync(_f, failures: 0, oldest: DateTime.UtcNow,
            tweak: m => m.ProcessedAtUtc = DateTime.UtcNow);
        var dead = await AddDeadProbeAsync();

        (await RetryAsync(admin, Guid.NewGuid(), 1)).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await admin.PostAsJsonAsync($"{ListUrl}/nosuchmodule/{dead}/retry", new { expectedAttempts = 1 }))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await RetryAsync(admin, pending, 3)).StatusCode.Should().Be(HttpStatusCode.Conflict, "ponawiane automatycznie — nie odłożone");
        (await RetryAsync(admin, processed, 0)).StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await admin.PostAsJsonAsync($"{ListUrl}/ordering/{dead}/retry", new { }))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);

        (await OutboxTestData.MessageAsync(_f, pending)).Attempts.Should().Be(3);
        (await OutboxTestData.MessageAsync(_f, dead)).DeadLetteredAtUtc.Should().NotBeNull();
        (await AuditCountAsync(dead)).Should().Be(0);
    }

    // ---------- Pomocnicze ----------

    /// <summary>Odłożone zdarzenie testowe z poprawnym ładunkiem (po usunięciu przyczyny da się je wysłać).</summary>
    private Task<Guid> AddDeadProbeAsync() => OutboxTestData.AddProbeAsync(_f, failures: 0, oldest: DateTime.UtcNow.AddYears(-5),
        tweak: m => { m.Attempts = 1; m.DeadLetteredAtUtc = DateTime.UtcNow; m.Error = OutboxErrorCategory.UnknownType; });

    private static Task<HttpResponseMessage> RetryAsync(HttpClient c, Guid id, int expectedAttempts)
        => c.PostAsJsonAsync($"{ListUrl}/ordering/{id}/retry", new { expectedAttempts });

    private async Task<List<ZipZap.Modules.Audit.Domain.AuditEntry>> AuditEntriesAsync(Guid id)
    {
        using var scope = _f.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AuditDbContext>();
        var key = id.ToString();
        return await db.Entries.AsNoTracking().Where(e => e.Action == "outbox.manual_retry" && e.EntityId == key).ToListAsync();
    }

    private async Task<int> AuditCountAsync(Guid id) => (await AuditEntriesAsync(id)).Count;
}
