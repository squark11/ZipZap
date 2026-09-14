using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Xunit;

namespace ZipZap.Api.IntegrationTests;

[Collection("api")]
public sealed class FeedbackTests
{
    private readonly ApiFactory _f;
    public FeedbackTests(ApiFactory f) => _f = f;

    private sealed record Submitted(Guid id);
    private sealed record Item(Guid id, string type, string message, string status);

    [Fact]
    public async Task Public_submits_feedback_and_admin_sees_it()
    {
        var msg = $"Uwaga testowa {Guid.NewGuid():N}";
        var submit = await _f.Anon().PostAsJsonAsync("/api/feedback",
            new { type = "Idea", message = msg, appVersion = "1.0.0", platform = "web", screen = "stores" });
        submit.EnsureSuccessStatusCode();
        (await submit.Content.ReadFromJsonAsync<Submitted>())!.id.Should().NotBe(Guid.Empty);

        var admin = await _f.LoginAdminAsync();
        var items = await _f.Authed(admin.accessToken).GetFromJsonAsync<Item[]>("/api/feedback?take=500");
        var mine = items!.Single(i => i.message == msg);
        mine.type.Should().Be("Idea");
        mine.status.Should().Be("New");
    }

    [Fact]
    public async Task Admin_updates_feedback_status()
    {
        var msg = $"Do zamkniecia {Guid.NewGuid():N}";
        var id = (await (await _f.Anon().PostAsJsonAsync("/api/feedback", new { type = "Bug", message = msg }))
            .Content.ReadFromJsonAsync<Submitted>())!.id;

        var admin = await _f.LoginAdminAsync();
        var c = _f.Authed(admin.accessToken);
        (await c.PatchAsync($"/api/feedback/{id}", JsonContent.Create(new { status = "Closed" }))).EnsureSuccessStatusCode();

        var closed = await c.GetFromJsonAsync<Item[]>("/api/feedback?status=Closed&take=500");
        closed!.Single(i => i.id == id).status.Should().Be("Closed");
    }

    [Fact]
    public async Task Customer_cannot_list_feedback()
    {
        var customer = await _f.RegisterCustomerAsync();
        var resp = await _f.Authed(customer.accessToken).GetAsync("/api/feedback");
        resp.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Empty_message_is_rejected()
    {
        var resp = await _f.Anon().PostAsJsonAsync("/api/feedback", new { type = "Bug", message = "   " });
        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
