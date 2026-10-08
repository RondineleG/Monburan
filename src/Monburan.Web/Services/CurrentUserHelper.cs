using System.Security.Claims;

namespace Monburan.Web.Services;

public sealed class CurrentUserHelper : ICurrentUserHelper
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUserHelper(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    private ClaimsPrincipal? User => _httpContextAccessor.HttpContext?.User;

    public Guid? GetCandidateId()
    {
        return User?.GetCandidateId();
    }

    public Guid? GetAdvertiserId()
    {
        return User?.GetAdvertiserId();
    }

    public string GetUserType()
    {
        return User?.GetUserType() ?? string.Empty;
    }

    public bool IsAuthenticated()
    {
        return User?.Identity?.IsAuthenticated ?? false;
    }
}
