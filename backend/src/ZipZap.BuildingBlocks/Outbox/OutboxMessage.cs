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

    /// <summary>Zserializowany JSON zdarzenia integracyjnego. Nigdy nie trafia do logów ani do panelu.</summary>
    public string Payload { get; set; } = default!;

    public DateTime OccurredAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? ProcessedAtUtc { get; set; }
    public int Attempts { get; set; }

    /// <summary>
    /// Kod kategorii ostatniego błędu (<see cref="OutboxErrorCategory"/>) — nigdy treść wyjątku ani ładunek.
    /// Wartości sprzed kategoryzacji panel pokazuje jako „ukryte" (<see cref="OutboxErrorCategory.Legacy"/>).
    /// </summary>
    public string? Error { get; set; }

    /// <summary>Najwcześniejszy moment kolejnej próby po błędzie przejściowym (backoff); null = od razu.</summary>
    public DateTime? NextAttemptAtUtc { get; set; }

    /// <summary>
    /// Dzierżawa: wiadomość pobrana przez procesor (tej lub innej instancji API) do tego momentu. Inne instancje ją
    /// pomijają; po awarii instancji dzierżawa wygasa i wiadomość wraca do kolejki (co najmniej raz).
    /// </summary>
    public DateTime? LockedUntilUtc { get; set; }

    /// <summary>
    /// Moment odłożenia do martwych po błędzie TRWAŁYM (nieznany typ zdarzenia, nieczytelny ładunek) — wiadomość nie
    /// jest już wysyłana, ale zostaje w bazie. Błędy przejściowe nigdy tu nie trafiają (są ponawiane bez limitu).
    /// Ponowienie ręczne: panel administratora (jedna wiadomość, audyt autora i czasu).
    /// </summary>
    public DateTime? DeadLetteredAtUtc { get; set; }

    /// <summary>Ostatnie ręczne ponowienie z panelu — kiedy i kto (zapisywane atomowo z samym ponowieniem).</summary>
    public DateTime? LastManualRetryAtUtc { get; set; }
    public Guid? LastManualRetryByUserId { get; set; }
}
