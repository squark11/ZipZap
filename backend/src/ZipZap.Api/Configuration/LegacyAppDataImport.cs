using System.Text.Json;
using System.Xml.Linq;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace ZipZap.Api.Configuration;

/// <summary>
/// Jednorazowe przeniesienie dawnych plików App_Data (konfiguracja z panelu + klucze Data Protection) do bazy.
/// Uruchamiane przy starcie PO migracjach, a PRZED pierwszym użyciem Data Protection (inaczej zaimportowane klucze
/// byłyby widoczne dopiero po odświeżeniu pierścienia kluczy). Wpis już obecny w bazie NIGDY nie jest nadpisywany,
/// pliki zostają nietknięte; nieczytelny plik jest pomijany z ostrzeżeniem (bez treści — mogą tam być sekrety).
/// </summary>
public static class LegacyAppDataImport
{
    /// <summary>Wyłącznik (testy: content root projektu może zawierać lokalne pliki dewelopera).</summary>
    public const string EnabledKey = "AppData:ImportLegacy";

    private static readonly (string File, string Key)[] Singles =
    {
        ("platform-settings.json", PlatformSettingsStore.Key),
        ("platform-integrations.json", PlatformIntegrationsStore.Key),
    };

    private static readonly (string File, string KeyPrefix)[] PerStore =
    {
        ("store-legal.json", StoreLegalStore.KeyPrefix),
        ("store-billing.json", StoreBillingStore.KeyPrefix),
        ("store-integrations.json", StoreIntegrationStore.KeyPrefix),
    };

    /// <summary>Importuje brakujące wpisy z katalogu <paramref name="appDataDir"/>; zwraca liczbę dodanych.</summary>
    public static async Task<int> RunAsync(IServiceProvider services, string appDataDir, ILogger logger, CancellationToken ct = default)
    {
        if (!Directory.Exists(appDataDir)) return 0;

        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformConfigDbContext>();
        var docs = new Dictionary<string, string>();

        foreach (var (file, key) in Singles)
        {
            var path = Path.Combine(appDataDir, file);
            if (File.Exists(path) && TryReadJson(path, logger) is JsonElement root && root.ValueKind == JsonValueKind.Object)
                docs[key] = root.GetRawText();
        }

        foreach (var (file, prefix) in PerStore)
        {
            var path = Path.Combine(appDataDir, file);
            if (!File.Exists(path) || TryReadJson(path, logger) is not JsonElement root || root.ValueKind != JsonValueKind.Object)
                continue;
            foreach (var entry in root.EnumerateObject())
                if (Guid.TryParse(entry.Name, out var storeId) && entry.Value.ValueKind == JsonValueKind.Object)
                    docs[prefix + storeId] = entry.Value.GetRawText();
        }

        var candidates = docs.Keys.ToList();
        var existing = candidates.Count == 0 ? new HashSet<string>()
            : (await db.Documents.AsNoTracking().Where(d => candidates.Contains(d.Key)).Select(d => d.Key).ToListAsync(ct)).ToHashSet();
        var added = 0;
        foreach (var (key, json) in docs)
        {
            if (existing.Contains(key)) continue;
            db.Documents.Add(new ConfigDocument(key, json, DateTime.UtcNow));
            added++;
        }

        var keysDir = Path.Combine(appDataDir, "keys");
        if (Directory.Exists(keysDir))
        {
            var known = (await db.DataProtectionKeys.AsNoTracking().Select(k => k.FriendlyName).ToListAsync(ct)).ToHashSet();
            foreach (var path in Directory.EnumerateFiles(keysDir, "*.xml"))
            {
                // Ta sama konwencja co repozytoria Data Protection: plik „key-{id}.xml" ↔ FriendlyName „key-{id}".
                var name = Path.GetFileNameWithoutExtension(path);
                if (known.Contains(name)) continue;
                XElement xml;
                try { xml = XElement.Load(path); }
                catch (Exception ex) when (ex is IOException or System.Xml.XmlException)
                {
                    logger.LogWarning("Import App_Data: pominięto nieczytelny klucz Data Protection {File} ({Error}).", name, ex.GetType().Name);
                    continue;
                }
                db.DataProtectionKeys.Add(new DataProtectionKey { FriendlyName = name, Xml = xml.ToString(SaveOptions.DisableFormatting) });
                added++;
            }
        }

        if (added == 0) return 0;
        await db.SaveChangesAsync(ct);
        logger.LogInformation("Import App_Data: przeniesiono do bazy {Count} wpis(ów) konfiguracji/kluczy (istniejące pominięte).", added);
        return added;
    }

    private static JsonElement? TryReadJson(string path, ILogger logger)
    {
        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(path));
            return doc.RootElement.Clone();
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            logger.LogWarning("Import App_Data: pominięto nieczytelny plik {File} ({Error}).", Path.GetFileName(path), ex.GetType().Name);
            return null;
        }
    }
}
