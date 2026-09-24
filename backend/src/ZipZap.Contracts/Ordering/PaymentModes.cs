namespace ZipZap.Contracts.Ordering;

/// <summary>
/// Tryb płatności zamówienia (zapisywany w chwili złożenia, przenoszony w <see cref="OrderPlaced"/>).
/// </summary>
public static class PaymentModes
{
    /// <summary>Płatność online przez bramkę (webhook-autorytatywna).</summary>
    public const string Online = "online";

    /// <summary>
    /// Pilotaż W1: zamówienie testowe zamkniętej grupy testerów — BEZ opłaty. Nie powstaje płatność,
    /// nie ma sesji, autoryzacji ani księgowania prowizji. Towar kupuje operator na własny rachunek.
    /// </summary>
    public const string Test = "test";

    public static bool IsKnown(string? mode) => mode is Online or Test;
}
