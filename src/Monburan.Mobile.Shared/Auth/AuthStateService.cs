using System.Net;

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
    /// ler estado parcial enquanto a primeira ainda está em voo. Uma inicialização que falhou não fica guardada: a
    /// próxima chamada tenta de novo (senão toda a área logada ficaria presa ao erro até fechar o app).
    /// </summary>
    public Task InitializeAsync()
    {
        var task = _initTask;
        if (task is null || task.IsFaulted || task.IsCanceled)
            _initTask = task = InitializeCoreAsync();
        return task;
    }

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
        catch (ApiException ex) when (ex.IsUnauthorized || ex.IsForbidden || ex.StatusCode == HttpStatusCode.NotFound)
        {
            // 401: sessão inválida. 403/404: conta sem perfil de candidato (anunciante, perfil apagado)
            await _tokens.ClearSessionAsync();
        }
        catch (ApiException)
        {
            // Offline, servidor em deploy (502), 5xx ou 429: falha temporária. A sessão salva continua valendo e o
            // perfil é carregado quando a pessoa abrir uma tela que precise dele
            IsAuthenticated = true;
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
        catch (ApiException ex) when (ex.IsForbidden || ex.StatusCode == HttpStatusCode.NotFound)
        {
            await _tokens.ClearSessionAsync();
            IsAuthenticated = false;
            CurrentCandidate = null;
            NotifyChanged();
            throw new ApiException(ex.StatusCode!.Value,
                "Este aplicativo é para candidatos. Esta conta não possui perfil de candidato.");
        }
        catch (ApiException ex) when (ex.IsUnauthorized)
        {
            // O token acabou de ser emitido e foi recusado: não deixa a sessão salva pela metade
            await _tokens.ClearSessionAsync();
            IsAuthenticated = false;
            CurrentCandidate = null;
            NotifyChanged();
            throw;
        }
        catch (ApiException)
        {
            // Offline ou servidor instável logo depois do login: deixa entrar pela sessão; o perfil será carregado depois
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
