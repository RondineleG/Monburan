using Monburan.Core.DTOs.Response;
using Monburan.Mobile.Shared.Api;

namespace Monburan.Mobile.Shared.Auth;

/// <summary>
/// Estado de autenticação do app: sessão persistida + perfil do candidato.
/// Componentes assinam <see cref="Changed"/> para reagir a login/logout.
/// </summary>
public class AuthStateService
{
    private readonly TokenService _tokens;
    private readonly MonburanApiClient _api;

    private Task? _initTask;

    public AuthStateService(TokenService tokens, MonburanApiClient api)
    {
        _tokens = tokens;
        _api = api;
        _tokens.SessionExpired += () =>
        {
            // Refresh falhou: a sessão morreu de verdade — limpa o estado para que
            // páginas protegidas redirecionem ao login em vez de mostrar UI autenticada
            IsAuthenticated = false;
            CurrentCandidate = null;
            NotifyChanged();
        };
    }

    public event Action? Changed;

    public bool IsAuthenticated { get; private set; }

    public CandidateResponse? CurrentCandidate { get; private set; }

    /// <summary>
    /// Carrega a sessão persistida e valida o perfil — chamar no start do app.
    /// Lazy-task: chamadas concorrentes compartilham a mesma inicialização em vez de
    /// ler estado parcial enquanto a primeira ainda está em voo.
    /// </summary>
    public Task InitializeAsync() => _initTask ??= InitializeCoreAsync();

    private async Task InitializeCoreAsync()
    {

        var session = await _tokens.GetSessionAsync();
        if (session is null)
            return;

        try
        {
            CurrentCandidate = await _api.GetMyCandidateAsync();
            IsAuthenticated = true;
        }
        catch (ApiException ex) when (ex.IsNetworkError)
        {
            // Offline no boot: considera autenticado pela sessão persistida
            IsAuthenticated = true;
        }
        catch (ApiException ex) when (ex.IsUnauthorized || ex.IsForbidden)
        {
            // 401: sessão inválida. 403: conta sem perfil de candidato (ex.: anunciante)
            await _tokens.ClearSessionAsync();
        }
        finally
        {
            NotifyChanged();
        }
    }

    public async Task SignInAsync(UserLoginResponse tokens)
    {
        await _tokens.SetSessionAsync(new AuthSession(tokens.AccessToken, tokens.RefreshToken));

        try
        {
            // O app é candidate-only — rejeita sessões sem perfil de candidato (403)
            CurrentCandidate = await _api.GetMyCandidateAsync();
            IsAuthenticated = true;
        }
        catch (ApiException ex) when (ex.IsForbidden)
        {
            await _tokens.ClearSessionAsync();
            IsAuthenticated = false;
            CurrentCandidate = null;
            NotifyChanged();
            throw new ApiException(ex.StatusCode!.Value,
                "Este aplicativo é para candidatos. Esta conta não possui perfil de candidato.");
        }
        catch (ApiException ex) when (ex.IsNetworkError)
        {
            // Offline: deixa entrar pela sessão; o perfil será carregado depois
            IsAuthenticated = true;
            CurrentCandidate = null;
        }

        NotifyChanged();
    }

    public async Task SignOutAsync()
    {
        await _tokens.ClearSessionAsync();
        IsAuthenticated = false;
        CurrentCandidate = null;
        NotifyChanged();
    }

    private void NotifyChanged() => Changed?.Invoke();
}
