namespace ZipZap.Api.Security;

/// <summary>
/// Reguły fail-fast dla środowisk publicznych: produkcja ORAZ publiczny pilotaż
/// (<c>Pilot:Public=true</c>), niezależnie od <c>ASPNETCORE_ENVIRONMENT</c>.
/// Zwraca listę problemów (pusta = OK). Nigdy nie zwraca wartości sekretów.
/// </summary>
public static class HardeningGuard
{
    /// <summary>Hasła, których administrator nie może mieć na środowisku publicznym.</summary>
    public static readonly IReadOnlyList<string> KnownDefaultAdminPasswords = new[] { "Admin123!" };

    public static bool IsPublicPilot(IConfiguration cfg) => cfg.GetValue<bool>("Pilot:Public", false);

    public static IReadOnlyList<string> Check(IConfiguration cfg, bool isProduction)
    {
        var publicPilot = IsPublicPilot(cfg);
        if (!isProduction && !publicPilot) return Array.Empty<string>(); // czysty dev — bez guardu

        var problems = new List<string>();

        // --- Wspólne dla każdego środowiska publicznego ---
        var key = cfg["Jwt:SigningKey"] ?? "";
        if (key.Length < 32
            || key.StartsWith("CHANGE_ME", StringComparison.OrdinalIgnoreCase)
            || key.Contains("DEV_ONLY", StringComparison.OrdinalIgnoreCase))
            problems.Add("Jwt:SigningKey — domyślny/deweloperski lub krótszy niż 32 znaki; ustaw losowy sekret (env: Jwt__SigningKey).");

        var conn = cfg.GetConnectionString("Postgres") ?? "";
        if (conn.Length == 0 || IsDefaultDbPassword(conn))
            problems.Add("ConnectionStrings:Postgres — domyślne hasło bazy; ustaw produkcyjne (env: ConnectionStrings__Postgres).");

        var rabbitUsed = !string.IsNullOrWhiteSpace(cfg["RabbitMq:Host"]);
        if ((isProduction || rabbitUsed) && (cfg["RabbitMq:Password"] ?? "") is "" or "zipzap")
            problems.Add("RabbitMq:Password — domyślne hasło brokera (env: RabbitMq__Password).");

        if (isProduction)
        {
            // Produkcja = realne pieniądze: wymagany realny dostawca, zero mocka.
            if ((cfg["Payments:Provider"] ?? "mock").Equals("mock", StringComparison.OrdinalIgnoreCase))
                problems.Add("Payments:Provider = 'mock' — na produkcji wymagany realny dostawca (env: Payments__Provider).");
            if ((cfg["Payments:Mock:Secret"] ?? "") == "mock-dev-secret")
                problems.Add("Payments:Mock:Secret — jawny w repozytorium sekret 'mock-dev-secret' (env: Payments__Mock__Secret).");
            var seedPwd = cfg["Seed:AdminPassword"] ?? "";
            if (seedPwd.Length > 0 && KnownDefaultAdminPasswords.Contains(seedPwd))
                problems.Add("Seed:AdminPassword — domyślne hasło administratora (env: Seed__AdminPassword).");
        }

        if (publicPilot)
        {
            // Pilotaż bez płatności: mock jest wyłączony w kodzie (brak dostawcy), więc nie wymagamy
            // realnej bramki. Wymagamy za to jawnego potwierdzenia właściciela, że istniejące konta
            // administratorów mają unikalne, silne hasła (seed jest pomijany i NIE zmienia haseł).
            if (!cfg.GetValue<bool>("Pilot:AdminPasswordConfirmed", false))
                problems.Add("Pilot:AdminPasswordConfirmed — potwierdź (true) po zmianie haseł administratorów na unikalne i silne " +
                             "(istniejące konto admin@zipzap.local NIE jest zmieniane przez pominięcie seeda).");
            if (cfg.GetValue<bool>("RateLimiting:Enabled", true) == false)
                problems.Add("RateLimiting:Enabled=false — w trybie publicznym limiter nie może być wyłączony.");
        }

        return problems;
    }

    /// <summary>Parsuje connection string (odporne na spacje/wielkość liter) i sprawdza domyślne hasło.</summary>
    private static bool IsDefaultDbPassword(string conn)
    {
        try
        {
            var b = new System.Data.Common.DbConnectionStringBuilder { ConnectionString = conn };
            return b.TryGetValue("Password", out var pw)
                   && string.Equals(pw?.ToString()?.Trim(), "zipzap", StringComparison.Ordinal);
        }
        catch (ArgumentException)
        {
            return true; // niepoprawny connection string traktujemy jako niebezpieczny
        }
    }
}
