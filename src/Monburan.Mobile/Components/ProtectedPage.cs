using Microsoft.AspNetCore.Components;

using Monburan.Mobile.Shared.Auth;

namespace Monburan.Mobile.Components;

/// <summary>
/// Base para páginas que exigem candidato autenticado.
/// Redireciona para /login quando não há sessão válida.
/// </summary>
public abstract class ProtectedPage : ComponentBase
{
    [Inject] protected AuthStateService Auth { get; set; } = default!;
    [Inject] protected NavigationManager Nav { get; set; } = default!;

    /// <summary>Garante sessão inicializada + autenticada; retorna false se redirecionou.</summary>
    protected async Task<bool> EnsureAuthenticatedAsync()
    {
        await Auth.InitializeAsync();
        if (Auth.IsAuthenticated)
            return true;

        Nav.NavigateTo("/login");
        return false;
    }
}
