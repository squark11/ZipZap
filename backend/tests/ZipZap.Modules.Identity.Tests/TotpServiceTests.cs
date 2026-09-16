using FluentAssertions;
using Xunit;
using ZipZap.Modules.Identity.Infrastructure;

namespace ZipZap.Modules.Identity.Tests;

public class TotpServiceTests
{
    // Wektor testowy RFC 6238 (App. B), SHA1, seed ASCII "12345678901234567890"
    // → Base32 "GEZDGNBVGY3TQOJQGEZDGNBVGY3TQOJQ". Kody 6-cyfrowe (końcówki wektorów 8-cyfrowych).
    private const string RfcSeed = "GEZDGNBVGY3TQOJQGEZDGNBVGY3TQOJQ";

    [Theory]
    [InlineData(1L, "287082")]          // T=59s        (TOTP8 94287082)
    [InlineData(37037036L, "081804")]   // T=1111111109 (TOTP8 07081804)
    [InlineData(37037037L, "050471")]   // T=1111111111 (TOTP8 14050471)
    public void GenerateCode_matches_rfc6238_vectors(long counter, string expected)
    {
        var svc = new TotpService();
        svc.GenerateCode(RfcSeed, counter).Should().Be(expected);
    }

    [Fact]
    public void Verify_accepts_freshly_generated_current_code()
    {
        var svc = new TotpService();
        var secret = svc.GenerateSecret();
        var counter = DateTimeOffset.UtcNow.ToUnixTimeSeconds() / 30;
        var code = svc.GenerateCode(secret, counter);

        svc.Verify(secret, code).Should().BeTrue();
    }

    [Fact]
    public void Verify_rejects_wrong_code_and_garbage()
    {
        var svc = new TotpService();
        var secret = svc.GenerateSecret();

        svc.Verify(secret, "000000").Should().BeFalse();
        svc.Verify(secret, "abc").Should().BeFalse();
        svc.Verify(secret, "").Should().BeFalse();
    }

    [Fact]
    public void GenerateSecret_is_valid_base32_and_unique()
    {
        var svc = new TotpService();
        var a = svc.GenerateSecret();
        var b = svc.GenerateSecret();

        a.Should().NotBe(b);
        a.Should().MatchRegex("^[A-Z2-7]+$");
    }

    [Fact]
    public void OtpAuthUri_carries_issuer_and_account()
    {
        var svc = new TotpService();
        var uri = svc.BuildOtpAuthUri("JBSWY3DPEHPK3PXP", "admin@dowozka.pl", "Dowozka.pl");

        uri.Should().StartWith("otpauth://totp/");
        uri.Should().Contain("secret=JBSWY3DPEHPK3PXP");
        uri.Should().Contain("issuer=Dowozka.pl");
        uri.Should().Contain("admin%40dowozka.pl");
    }
}
