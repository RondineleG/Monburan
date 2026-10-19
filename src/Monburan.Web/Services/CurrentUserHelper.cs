using System.Security.Claims;

using Microsoft.Extensions.Options;

namespace Monburan.Web.Services;

public sealed class CurrentUserHelper : ICurrentUserHelper
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly CircuitUserAccessor _circuitUser;

    private readonly IOptionsMonitor<SecurityOptions> _security;

    public CurrentUserHelper(IHttpContextAccessor httpContextAccessor, CircuitUserAccessor circuitUser, IOptionsMonitor<SecurityOptions> security)
    {
        _httpContextAccessor = httpContextAccessor;
        _circuitUser = circuitUser;
        _security = security;
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

    public bool IsAdmin()
    {
        var user = User;
        return user is not null
            && user.IsInRole("Admin")
            && (!_security.CurrentValue.RequireTwoFactorForAdmins || AdminTwoFactorHandler.HasTwoFactor(user));
    }
}
