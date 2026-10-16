using Monburan.Core.DTOs.Request;
using Monburan.Core.DTOs.Response;

namespace Monburan.Mobile.Shared.Api;

/// <summary>
/// Chamadas de autenticação (login, registro, esqueci a senha) — usa o cliente
/// HTTP sem bearer para não acionar o fluxo de refresh em 401.
/// </summary>
public class AuthApiClient
{
    private readonly HttpClient _http;

    public AuthApiClient(IHttpClientFactory httpClientFactory)
    {
        _http = httpClientFactory.CreateClient(ApiClientNames.Auth);
    }

    public Task<UserLoginResponse> LoginAsync(string email, string password, CancellationToken ct = default) =>
        MonburanApiClient.SendAsync<UserLoginResponse>(_http, HttpMethod.Post, "api/auth/login",
            new UserLoginRequest { Email = email, Password = password }, ct);

    public Task<UserLoginResponse> RegisterAsync(RegisterRequest request, CancellationToken ct = default) =>
        MonburanApiClient.SendAsync<UserLoginResponse>(_http, HttpMethod.Post, "api/auth/register", request, ct);

    public Task ForgotPasswordAsync(string email, CancellationToken ct = default) =>
        MonburanApiClient.SendAsync<object>(_http, HttpMethod.Post, "api/auth/forgot-password",
            new ForgotPasswordRequest { Email = email }, ct);
}
