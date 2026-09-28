namespace ZipZap.BuildingBlocks.Outbox;

/// <summary>Uzgodnione reguły outboxa (wspólne dla procesora, panelu i dokumentacji).</summary>
public static class OutboxPolicy
{
    /// <summary>Tyle wiadomości procesor pobiera naraz (na moduł).</summary>
    public const int BatchSize = 20;

    /// <summary>Błąd przejściowy: odstęp 10 s, 20 s, 40 s… maks. co 30 min, BEZ limitu prób (decyzja właściciela).</summary>
    public static readonly TimeSpan BaseRetryDelay = TimeSpan.FromSeconds(10);
    public static readonly TimeSpan MaxRetryDelay = TimeSpan.FromMinutes(30);

    /// <summary>Dzierżawa pobranej partii (ochrona przed równoległą wysyłką przez kilka instancji API).</summary>
    public static readonly TimeSpan LeaseDuration = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Od tylu nieudanych prób panel pokazuje wiadomość jako „ponawianą długo" (ok. 1,5 h prób). To tylko sygnał —
    /// wiadomość NADAL jest ponawiana i NIE trafia do martwych.
    /// </summary>
    public const int LongRetryAttempts = 10;
}
