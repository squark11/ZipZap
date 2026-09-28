namespace ZipZap.BuildingBlocks.Outbox;

/// <summary>
/// Wiadomość outboxa. Zapisywana w TEJ SAMEJ transakcji co zmiana domenowa,
/// dzięki czemu publikacja zdarzenia jest niezawodna (at-least-once).
/// Każdy moduł mapuje tę encję do własnego schematu (tabela `outbox_messages`).
/// </summary>
public sealed class OutboxMessage
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Nazwa typu zdarzenia z rejestru (do deserializacji ładunku).</summary>
    public string Type { get; set; } = default!;

    /// <summary>Zserializowany JSON zdarzenia integracyjnego.</summary>
    public string Payload { get; set; } = default!;

    public DateTime OccurredAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? ProcessedAtUtc { get; set; }
    public int Attempts { get; set; }
    public string? Error { get; set; }

    /// <summary>Najwcześniejszy moment kolejnej próby po błędzie (backoff); null = od razu.</summary>
    public DateTime? NextAttemptAtUtc { get; set; }

    /// <summary>
    /// Moment odłożenia do martwych po błędzie TRWAŁYM (nieznany typ zdarzenia, nieczytelny ładunek) — wiadomość nie
    /// jest już wysyłana, ale zostaje w bazie. Błędy przejściowe nigdy tu nie trafiają (są ponawiane bez limitu).
    /// Ponowienie ręczne: wyzerować DeadLetteredAtUtc, NextAttemptAtUtc i Attempts.
    /// </summary>
    public DateTime? DeadLetteredAtUtc { get; set; }
}
