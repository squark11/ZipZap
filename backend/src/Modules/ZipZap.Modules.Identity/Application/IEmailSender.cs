namespace ZipZap.Modules.Identity.Application;

public sealed record EmailMessage(string To, string Subject, string Body);

/// <summary>
/// Port wysyłki e-maili. Moduł ma mock logujący (<c>LoggingEmailSender</c>); host nadpisuje go realnym
/// senderem (HTTP API dostawcy / SMTP).
/// </summary>
public interface IEmailSender
{
    Task SendAsync(EmailMessage message, CancellationToken ct = default);
}

/// <summary>Którym kanałem idzie poczta: <c>http:&lt;dostawca&gt;</c>, <c>smtp</c> albo <c>none</c> (atrapa — nic nie jest wysyłane).</summary>
public interface IEmailChannelInfo
{
    Task<string> GetChannelAsync(CancellationToken ct = default);
}

public static class EmailChannel
{
    public const string None = "none";

    /// <summary>Publiczny opis dla aplikacji: „live" albo „mock" (atrapa) — bez szczegółów konfiguracji.</summary>
    public static string PublicDelivery(string channel) => channel == None ? "mock" : "live";
}
