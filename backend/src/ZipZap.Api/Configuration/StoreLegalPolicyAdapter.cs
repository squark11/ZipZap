using ZipZap.Modules.Ordering.Application;

namespace ZipZap.Api.Configuration;

/// <summary>
/// Adapter portu <see cref="IStoreLegalPolicyProvider"/> nad magazynem dokumentów sklepów.
/// Udostępnia modułowi Ordering wymóg akceptacji i adresy dokumentów (bez sięgania do hosta).
/// </summary>
public sealed class StoreLegalPolicyAdapter : IStoreLegalPolicyProvider
{
    private readonly StoreLegalStore _store;
    public StoreLegalPolicyAdapter(StoreLegalStore store) => _store = store;

    public async Task<StoreLegalPolicy?> GetAsync(Guid storeId, CancellationToken ct = default)
    {
        var l = await _store.GetAsync(storeId, ct);
        var empty = !l.RequiresAcceptance
            && string.IsNullOrWhiteSpace(l.TermsUrl)
            && string.IsNullOrWhiteSpace(l.PrivacyUrl)
            && string.IsNullOrWhiteSpace(l.GdprUrl);
        return empty ? null : new StoreLegalPolicy(l.RequiresAcceptance, l.TermsUrl, l.PrivacyUrl, l.GdprUrl);
    }
}
