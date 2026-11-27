using System.Security.Claims;

namespace Monburan.Core.Interfaces.Services;

/// <summary>Usuário autenticado do boundary atual (HTTP ou circuito Blazor), quando existir.</summary>
public interface ICurrentPrincipalAccessor
{
    ClaimsPrincipal? User { get; }
}
