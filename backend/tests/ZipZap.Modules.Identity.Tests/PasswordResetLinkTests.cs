using FluentAssertions;
using Xunit;
using ZipZap.Modules.Identity.Application;

namespace ZipZap.Modules.Identity.Tests;

public class PasswordResetLinkTests
{
    [Theory]
    [InlineData("https://panel.dowózka.pl", "https://panel.xn--dowzka-dxa.pl")]
    [InlineData("https://panel.dowózka.pl/", "https://panel.xn--dowzka-dxa.pl")]
    [InlineData("https://panel.xn--dowzka-dxa.pl", "https://panel.xn--dowzka-dxa.pl")]
    [InlineData("http://localhost:4200", "http://localhost:4200")]
    [InlineData(" https://example.com/panel/ ", "https://example.com/panel")]
    public void Links_in_emails_use_the_punycode_host(string publicUrl, string expected)
        => IdentityService.PublicBaseUrl(publicUrl).Should().Be(expected);
}
