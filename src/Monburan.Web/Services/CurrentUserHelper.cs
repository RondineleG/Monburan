using System.Security.Claims;

namespace Monburan.Web.Services;

public sealed class CurrentUserHelper : ICurrentUserHelper
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly CircuitUserAccessor _circuitUser;

    public CurrentUserHelper(IHttpContextAccessor httpContextAccessor, CircuitUserAccessor circuitUser)
    {
        _httpContextAccessor = httpContextAccessor;
        _circuitUser = circuitUser;
    }

    // Circuito interativo: usuário do AuthenticationStateProvider. Requisições HTTP
    // (API, SSR, pré-renderização): usuário da requisição.
    private ClaimsPrincipal? User => _circuitUser.User ?? _httpContextAccessor.HttpContext?.User;

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
