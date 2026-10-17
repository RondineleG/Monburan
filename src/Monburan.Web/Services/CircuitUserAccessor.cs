using System.Security.Claims;

using Microsoft.AspNetCore.Components.Server.Circuits;

namespace Monburan.Web.Services;

/// <summary>
/// Usuário do circuito Blazor interativo. Dentro de um circuito o <c>HttpContext</c> não é
/// confiável (a Microsoft desaconselha IHttpContextAccessor em renderização interativa);
/// o usuário vem do <see cref="AuthenticationStateProvider"/> e é guardado aqui.
/// </summary>
public sealed class CircuitUserAccessor
{
    public ClaimsPrincipal? User { get; internal set; }
}

/// <summary>Mantém <see cref="CircuitUserAccessor"/> atualizado durante a vida do circuito.</summary>
internal sealed class UserCircuitHandler : CircuitHandler, IDisposable
{
    private readonly AuthenticationStateProvider _authenticationStateProvider;
    private readonly CircuitUserAccessor _userAccessor;
    private readonly ILogger<UserCircuitHandler> _logger;

    public UserCircuitHandler(
        AuthenticationStateProvider authenticationStateProvider,
        CircuitUserAccessor userAccessor,
        ILogger<UserCircuitHandler> logger)
    {
        _authenticationStateProvider = authenticationStateProvider;
        _userAccessor = userAccessor;
        _logger = logger;
    }

    public override Task OnCircuitOpenedAsync(Circuit circuit, CancellationToken cancellationToken)
    {
        _authenticationStateProvider.AuthenticationStateChanged += OnAuthenticationChanged;
        return base.OnCircuitOpenedAsync(circuit, cancellationToken);
    }

    // Executa antes de os componentes do circuito renderizarem
    public override async Task OnConnectionUpAsync(Circuit circuit, CancellationToken cancellationToken)
    {
        var state = await _authenticationStateProvider.GetAuthenticationStateAsync();
        _userAccessor.User = state.User;
    }

    private void OnAuthenticationChanged(Task<AuthenticationState> task) => _ = UpdateUserAsync(task);

    private async Task UpdateUserAsync(Task<AuthenticationState> task)
    {
        try
        {
            _userAccessor.User = (await task).User;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Falha ao atualizar o usuário do circuito após mudança de autenticação");
        }
    }

    public void Dispose() => _authenticationStateProvider.AuthenticationStateChanged -= OnAuthenticationChanged;
}
