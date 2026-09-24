using ZipZap.Contracts.Ordering;

namespace ZipZap.Modules.Ordering.Application;

/// <summary>Ustawienia pilotażu dla zamówień (sekcja konfiguracji <c>Pilot</c>).</summary>
public sealed class PilotOrderingOptions
{
    public const string SectionName = "Pilot";

    /// <summary>
    /// <c>online</c> (domyślnie) albo <c>test</c> — pilotaż W1: zamówienia bez opłaty, wyłącznie dla
    /// kont z rolą <c>Tester</c> (lub Admin do kontroli operacyjnej).
    /// </summary>
    public string PaymentMode { get; set; } = PaymentModes.Online;

    /// <summary>Limit zamówień testowych jednego testera w ciągu 24 h (ogranicza koszt towaru). 0 = bez limitu.</summary>
    public int TestOrdersPerTesterPerDay { get; set; } = 3;
}
