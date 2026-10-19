using System.Security.Claims;

using Monburan.Core.Interfaces.Services;

namespace Monburan.Web.Services;

public static class AdminActorExtensions
{
    /// <summary>Quem está logado, a partir das claims da própria sessão (nunca de um valor enviado pelo navegador).</summary>
    public static AdminActor ToAdminActor(this ClaimsPrincipal user) => new(
        user.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty,
        user.FindFirstValue(ClaimTypes.Email) ?? user.Identity?.Name ?? "desconhecido");
}
