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
    /// Só <see cref="RefreshOutcome.Rejected"/> encerra a sessão: sem rede, tempo esgotado, 5xx ou 429 devolvem
    /// <see cref="RefreshOutcome.Unavailable"/> e a pessoa continua logada (o refresh token vale 30 dias).
    /// </summary>
    public async Task<RefreshOutcome> RefreshAsync(CancellationToken cancellationToken = default)
    {
        await EnsureLoadedAsync();
        if (_session is null)
            return RefreshOutcome.Rejected;

        var expired = false;
        var sessionAtEntry = _session;
        await _refreshLock.WaitAsync(cancellationToken);
        try
        {
            var current = _session;
            if (current is null)
                return RefreshOutcome.Rejected;

            // Outra thread pode já ter renovado enquanto esperávamos o lock —
            // se a sessão mudou, reusamos o resultado em vez de renovar de novo
            if (!ReferenceEquals(current, sessionAtEntry))
                return RefreshOutcome.Renewed;

            using var response = await _authClient.PostAsJsonAsync(
                "api/auth/refresh",
                new RefreshTokenRequest { RefreshToken = current.RefreshToken },
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                // Só destrói a sessão quando o refresh foi efetivamente rejeitado;
                // 5xx/429/indisponibilidade mantém a sessão para nova tentativa depois
                if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.BadRequest)
                {
                    await ClearSessionAsync();
                    expired = true;
                    return RefreshOutcome.Rejected;
                }
                return RefreshOutcome.Unavailable;
            }

            var body = await response.Content.ReadFromJsonAsync<UserLoginResponse>(JsonOptions, cancellationToken);
            if (string.IsNullOrEmpty(body?.AccessToken) || string.IsNullOrEmpty(body.RefreshToken))
            {
                await ClearSessionAsync();
                expired = true;
                return RefreshOutcome.Rejected;
            }

            // Saiu da conta (ou entrou em outra) enquanto o refresh viajava: não ressuscita a sessão anterior
            if (!ReferenceEquals(_session, current))
                return _session is null ? RefreshOutcome.Rejected : RefreshOutcome.Renewed;

            await SetSessionAsync(new AuthSession(body.AccessToken, body.RefreshToken));
            return RefreshOutcome.Renewed;
        }
        catch (JsonException)
        {
            // Resposta de refresh malformada: sessão não é mais confiável
            await ClearSessionAsync();
            expired = true;
            return RefreshOutcome.Rejected;
        }
        catch (Exception ex) when (ApiException.IsTransportFailure(ex)
            || (ex is TaskCanceledException && !cancellationToken.IsCancellationRequested))
        {
            // Offline/timeout/queda no meio: mantém a sessão — o usuário tenta de novo quando voltar a rede
            return RefreshOutcome.Unavailable;
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
