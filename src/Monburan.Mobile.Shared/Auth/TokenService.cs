using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using Monburan.Core.DTOs.Request;
using Monburan.Core.DTOs.Response;
using Monburan.Mobile.Shared.Api;

namespace Monburan.Mobile.Shared.Auth;

/// <summary>
/// Gerencia a sessão de tokens: cache em memória, persistência via <see cref="ITokenStore"/>
/// e renovação transparente do access token usando o refresh token.
/// </summary>
public class TokenService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly ITokenStore _store;
    private readonly HttpClient _authClient;
    private readonly SemaphoreSlim _refreshLock = new(1, 1);

    private AuthSession? _session;
    private bool _loaded;

    public TokenService(ITokenStore store, IHttpClientFactory httpClientFactory)
    {
        _store = store;
        // Cliente sem o AuthHeaderHandler — evita loop de refresh dentro do refresh
        _authClient = httpClientFactory.CreateClient(ApiClientNames.Auth);
    }

    /// <summary>Evento disparado quando a sessão é perdida (refresh falhou).</summary>
    public event Action? SessionExpired;

    public async Task<AuthSession?> GetSessionAsync()
    {
        await EnsureLoadedAsync();
        return _session;
    }

    public async Task<string?> GetAccessTokenAsync() => (await GetSessionAsync())?.AccessToken;

    public async Task SetSessionAsync(AuthSession session)
    {
        _session = session;
        _loaded = true;
        await _store.SaveAsync(session);
    }

    public async Task ClearSessionAsync()
    {
        _session = null;
        _loaded = true;
        await _store.ClearAsync();
    }

    /// <summary>
    /// Tenta renovar a sessão com o refresh token atual. Thread-safe:
    /// chamadas concorrentes compartilham a mesma renovação.
    /// </summary>
    public async Task<bool> RefreshAsync(CancellationToken cancellationToken = default)
    {
        await EnsureLoadedAsync();
        if (_session is null)
            return false;

        var expired = false;
        var sessionAtEntry = _session;
        await _refreshLock.WaitAsync(cancellationToken);
        try
        {
            var current = _session;
            if (current is null)
                return false;

            // Outra thread pode já ter renovado enquanto esperávamos o lock —
            // se a sessão mudou, reusamos o resultado em vez de renovar de novo
            if (!ReferenceEquals(current, sessionAtEntry))
                return true;

            using var response = await _authClient.PostAsJsonAsync(
                "api/auth/refresh",
                new RefreshTokenRequest { RefreshToken = current.RefreshToken },
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                // Só destrói a sessão quando o refresh foi efetivamente rejeitado;
                // 5xx/indisponibilidade mantém a sessão para nova tentativa depois
                if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.BadRequest)
                {
                    await ClearSessionAsync();
                    expired = true;
                }
                return false;
            }

            var body = await response.Content.ReadFromJsonAsync<UserLoginResponse>(JsonOptions, cancellationToken);
            if (body?.AccessToken is null || body.RefreshToken is null)
            {
                await ClearSessionAsync();
                expired = true;
                return false;
            }

            await SetSessionAsync(new AuthSession(body.AccessToken, body.RefreshToken));
            return true;
        }
        catch (JsonException)
        {
            // Resposta de refresh malformada: sessão não é mais confiável
            await ClearSessionAsync();
            expired = true;
            return false;
        }
        catch (Exception ex) when (ex is HttpRequestException
            or TaskCanceledException { CancellationToken.IsCancellationRequested: false })
        {
            // Offline/timeout: mantém a sessão — o usuário tenta de novo quando voltar a rede
            return false;
        }
        finally
        {
            _refreshLock.Release();
            // Evento fora do lock — um subscriber que chame RefreshAsync não deadloca
            if (expired)
                SessionExpired?.Invoke();
        }
    }

    private async Task EnsureLoadedAsync()
    {
        if (_loaded)
            return;

        // Primeira leitura concorrente: duas threads não devem carregar a loja
        // duas vezes nem observar estado parcial — reusa o semáforo do refresh
        await _refreshLock.WaitAsync();
        try
        {
            if (!_loaded)
            {
                _session = await _store.LoadAsync();
                _loaded = true;
            }
        }
        finally
        {
            _refreshLock.Release();
        }
    }
}
