using System.Security.Claims;
using ZipZap.BuildingBlocks.MultiTenancy;

namespace ZipZap.Api.Security;

/// <summary>Implementacja <see cref="ICurrentUser"/> czytająca claimy z JWT.</summary>
public sealed class HttpContextCurrentUser : ICurrentUser
{
    private readonly IHttpContextAccessor _accessor;

    public HttpContextCurrentUser(IHttpContextAccessor accessor) => _accessor = accessor;

    private ClaimsPrincipal? Principal => _accessor.HttpContext?.User;

    public Guid? UserId
    {
        get
        {
            var value = Principal?.FindFirstValue("sub") ?? Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
            return Guid.TryParse(value, out var id) ? id : null;
        }
    }

    public Guid? StoreId =>
        Guid.TryParse(Principal?.FindFirstValue("store_id"), out var storeId) ? storeId : null;

    public IReadOnlyCollection<Guid> StoreIds => GuidClaims("store_id");

    public IReadOnlyCollection<Guid> DriverStoreIds => GuidClaims("driver_store_id");

    private IReadOnlyCollection<Guid> GuidClaims(string type) =>
        Principal?.FindAll(type)
            .Select(c => Guid.TryParse(c.Value, out var id) ? id : (Guid?)null)
            .Where(id => id.HasValue).Select(id => id!.Value).Distinct().ToArray()
        ?? Array.Empty<Guid>();

    public IReadOnlyCollection<string> Roles =>
        Principal?.FindAll(ClaimTypes.Role).Select(c => c.Value).Distinct().ToArray() ?? Array.Empty<string>();

    public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated ?? false;
}
