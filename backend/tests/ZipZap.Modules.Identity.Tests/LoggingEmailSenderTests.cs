using FluentAssertions;
using Microsoft.Extensions.Logging;
using Xunit;
using ZipZap.Modules.Identity.Application;
using ZipZap.Modules.Identity.Infrastructure;

namespace ZipZap.Modules.Identity.Tests;

public class LoggingEmailSenderTests
{
    private sealed class CapturingLogger<T> : ILogger<T>
    {
        public List<string> Lines { get; } = new();
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter) => Lines.Add(formatter(state, exception));
    }

    [Fact]
    public async Task Never_logs_message_body_with_tokens_or_full_recipient()
    {
        var logger = new CapturingLogger<LoggingEmailSender>();
        var sender = new LoggingEmailSender(logger);
        const string token = "RESET-TOKEN-4f9c1a7e";

        await sender.SendAsync(new EmailMessage("jan.kowalski@example.com", "Reset hasła",
            $"Otwórz: https://app/reset-password?token={token}"));

        var all = string.Join("\n", logger.Lines);
        all.Should().NotContain(token);
        all.Should().NotContain("reset-password?token");
        all.Should().NotContain("jan.kowalski@example.com");
        all.Should().Contain("j***@example.com");
    }
}
