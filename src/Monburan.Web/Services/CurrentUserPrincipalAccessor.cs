using System.Security.Claims;

using Monburan.Core.Interfaces.Services;

namespace Monburan.Web.Services;

public sealed class CurrentUserPrincipalAccessor(
    IHttpContextAccessor httpContextAccessor,
    CircuitUserAccessor circuitUser) : ICurrentPrincipalAccessor
{
    public ClaimsPrincipal? User => circuitUser.User ?? httpContextAccessor.HttpContext?.User;
}
