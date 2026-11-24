namespace ZipZap.Modules.Catalog.Domain;

/// <summary>
/// Wspólna kategoria platformy (decyzja F-6), np. „owoce". Klient filtruje nią sklepy na Starcie; sklep przypina do
/// niej własne kategorie. Klucz = nazwa ikony w aplikacji (`assets/svg/categories/{Key}.svg`), więc się nie zmienia;
/// administrator zmienia tylko nazwę, kolejność i widoczność.
/// </summary>
public sealed class PlatformCategory
{
    public const int KeyMaxLength = 32;
    public const int NameMaxLength = 60;

    public string Key { get; private set; } = default!;
    public string Name { get; private set; } = default!;
    public int SortOrder { get; private set; }
    public bool IsActive { get; private set; }

    private PlatformCategory() { } // EF

    public PlatformCategory(string key, string name, int sortOrder, bool isActive = true)
    {
        Key = key;
        Name = name;
        SortOrder = sortOrder;
        IsActive = isActive;
    }

    public void Rename(string name) => Name = name.Trim();
    public void Reorder(int sortOrder) => SortOrder = sortOrder;
    public void SetActive(bool active) => IsActive = active;

    /// <summary>Lista startowa — klucze odpowiadają ikonom dołączonym do aplikacji.</summary>
    public static readonly IReadOnlyList<PlatformCategory> Seed = new[]
    {
        new PlatformCategory("owoce", "Owoce", 10),
        new PlatformCategory("warzywa", "Warzywa", 20),
        new PlatformCategory("pieczywo", "Pieczywo", 30),
        new PlatformCategory("nabial", "Nabiał", 40),
        new PlatformCategory("mieso", "Mięso i wędliny", 50),
        new PlatformCategory("ryby", "Ryby", 60),
        new PlatformCategory("mrozonki", "Mrożonki", 70),
        new PlatformCategory("napoje", "Napoje", 80),
        new PlatformCategory("slodycze", "Słodycze i przekąski", 90),
        new PlatformCategory("chemia", "Chemia i higiena", 100),
        new PlatformCategory("dziecko", "Dla dziecka", 110),
    };
}
