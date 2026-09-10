namespace ZipZap.Modules.Payments.Application;

public sealed class PaymentsOptions
{
    public const string SectionName = "Payments";

    /// <summary>Domyślny dostawca do tworzenia sesji (klucz, np. "mock", "przelewy24").</summary>
    public string Provider { get; set; } = "mock";

    /// <summary>Bazowy URL (strona płatności / powrót).</summary>
    public string PublicUrl { get; set; } = "http://localhost:4200";

    public MockProviderOptions Mock { get; set; } = new();

    public sealed class MockProviderOptions
    {
        /// <summary>Sekret do podpisu HMAC webhooków mocka (dev/test).</summary>
        public string Secret { get; set; } = "mock-dev-secret";

        /// <summary>
        /// Bazowy URL dev-owej strony „płatności" hostowanej przez API
        /// (np. http://localhost:5080/api/payments/mock/pay). Gdy puste — redirect
        /// wskazuje `{PublicUrl}/pay/mock` (zachowanie zgodne wstecz).
        /// </summary>
        public string PayPageUrl { get; set; } = "";
    }
}
