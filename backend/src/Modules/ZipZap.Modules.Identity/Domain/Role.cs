namespace ZipZap.Modules.Identity.Domain;

/// <summary>Role RBAC platformy ZipZap.</summary>
public enum Role
{
    Customer,        // klient
    StoreEmployee,   // pracownik sklepu (scope: StoreId)
    Driver,          // kierowca (scope: StoreId)
    Admin            // administrator platformy
}
