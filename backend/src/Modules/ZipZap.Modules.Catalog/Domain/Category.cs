using ZipZap.BuildingBlocks.Domain;

namespace ZipZap.Modules.Catalog.Domain;

/// <summary>Kategoria produktów w obrębie sklepu (multi-tenant: StoreId).</summary>
public sealed class Category : Entity
{
    public Guid StoreId { get; private set; }
    public string Name { get; private set; } = default!;
    public int SortOrder { get; private set; }
    public Guid? ParentId { get; private set; }

    /// <summary>Kategoria platformy, do której sklep przypiął tę kategorię (F-6); null = brak.</summary>
    public string? PlatformCategoryKey { get; private set; }

    private Category() { } // EF

    public Category(Guid storeId, string name, int sortOrder, Guid? parentId)
    {
        StoreId = storeId;
        Name = name.Trim();
        SortOrder = sortOrder;
        ParentId = parentId;
    }

    public void Rename(string name) => Name = name.Trim();
    public void Reorder(int sortOrder) => SortOrder = sortOrder;
    public void SetPlatformCategory(string? key) => PlatformCategoryKey = key;
}
