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

    public IReadOnlyCollection<string> Roles =>
        Principal?.FindAll(ClaimTypes.Role).Select(c => c.Value).Distinct().ToArray() ?? Array.Empty<string>();

    public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated ?? false;
}
