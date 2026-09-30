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

/// <summary>
/// Kolejka e-maili wysyłanych w tle (link resetu hasła). Odpowiedź API nie czeka na dostawcę poczty, więc czas
/// odpowiedzi i ewentualny błąd wysyłki nie zdradzają, czy konto istnieje. Kolejka jest w pamięci procesu —
/// po restarcie niewysłana wiadomość przepada (użytkownik może poprosić o link ponownie).
/// </summary>
public interface IEmailQueue
{
    void Enqueue(EmailMessage message);
}

/// <summary>Przyjmuje prośbę o link resetu hasła do przetworzenia w tle (stały czas odpowiedzi endpointu).</summary>
public interface IPasswordResetRequestQueue
{
    void Enqueue(string? email);
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
