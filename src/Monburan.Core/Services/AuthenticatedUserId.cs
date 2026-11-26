using System.Security.Claims;

namespace Monburan.Core.Services;

public static class AuthenticatedUserId
{
    public static string? From(ClaimsPrincipal? user) =>
        user?.FindFirst(ClaimTypes.NameIdentifier)?.Value
        ?? user?.FindFirst("sub")?.Value;
}
