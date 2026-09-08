using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Xunit;
using ZipZap.BuildingBlocks.Messaging;
using ZipZap.Modules.Identity.Application;
using ZipZap.Modules.Identity.Domain;
using ZipZap.Modules.Identity.Infrastructure;

namespace ZipZap.Modules.Identity.Tests;

public class GoogleLoginTests
{
    private static IdentityDbContext NewDb()
        => new(new DbContextOptionsBuilder<IdentityDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static IdentityService Build(IdentityDbContext db, GoogleUserInfo? googlePayload)
        => new(
            db,
            new Pbkdf2PasswordHasher(),
            new FakeTokenService(),
            new IntegrationEventTypeRegistry(),
            new FakeEmailSender(),
            Options.Create(new IdentityOptions()),
            new FakeGoogleTokenValidator(googlePayload));

    [Fact]
    public async Task Unknown_google_email_creates_verified_customer()
    {
        using var db = NewDb();
        var svc = Build(db, new GoogleUserInfo("sub-1", "new@ex.com", "New User", EmailVerified: true));

        var result = await svc.LoginWithGoogleAsync("token", default);

        result.IsSuccess.Should().BeTrue();
        result.Value.User.Email.Should().Be("new@ex.com");
        result.Value.User.Roles.Should().Contain("Customer");

        var user = await db.Users.SingleAsync();
        user.IsEmailVerified.Should().BeTrue();
    }

    [Fact]
    public async Task Known_email_links_existing_user_without_duplicate()
    {
        using var db = NewDb();
        var existing = User.Register("known@ex.com", new Pbkdf2PasswordHasher().Hash("pw123456"), "Known", null, Role.Customer);
        db.Users.Add(existing);
        await db.SaveChangesAsync();

        var svc = Build(db, new GoogleUserInfo("sub-2", "Known@Ex.com", "Known G", EmailVerified: true));
        var result = await svc.LoginWithGoogleAsync("token", default);

        result.IsSuccess.Should().BeTrue();
        result.Value.User.Id.Should().Be(existing.Id);
        (await db.Users.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task Invalid_token_is_rejected()
    {
        using var db = NewDb();
        var svc = Build(db, googlePayload: null); // walidator zwraca null

        var result = await svc.LoginWithGoogleAsync("bad", default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("unauthorized");
    }

    [Fact]
    public async Task Unverified_google_email_is_rejected()
    {
        using var db = NewDb();
        var svc = Build(db, new GoogleUserInfo("sub-3", "x@ex.com", "X", EmailVerified: false));

        var result = await svc.LoginWithGoogleAsync("token", default);

        result.IsFailure.Should().BeTrue();
        (await db.Users.CountAsync()).Should().Be(0);
    }
}
