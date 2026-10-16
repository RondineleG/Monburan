using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

using FluentAssertions;

using Microsoft.Extensions.DependencyInjection;

using Monburan.Core.DTOs.Request;
using Monburan.Core.DTOs.Response;

namespace Monburan.ApiTests;

public class AuthApiTests : IClassFixture<MonburanApiFactory>
{
    private readonly HttpClient _client;
    private readonly MonburanApiFactory _factory;

    public AuthApiTests(MonburanApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Login_WithValidCredentials_ReturnsTokens()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/login", new UserLoginRequest
        {
            Email = "admin@monburan.com",
            Password = "Monburan123"
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<UserLoginResponse>();
        body.Should().NotBeNull();
        body!.Sucesso.Should().BeTrue();
        body.AccessToken.Should().NotBeNullOrEmpty();
        body.RefreshToken.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Login_WithWrongPassword_Returns401()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/login", new UserLoginRequest
        {
            Email = "admin@monburan.com",
            Password = "senha-errada"
        });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ProtectedEndpoint_WithoutToken_Returns401()
    {
        var response = await _client.GetAsync("/api/v1/applications/mine");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ProtectedEndpoint_WithRefreshToken_Returns403()
    {
        // Refresh token é um JWT válido, mas sem token_use=access — não deve autenticar
        var login = await LoginAsAsync("admin@monburan.com");

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/applications/mine");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", login.RefreshToken);

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task ProtectedEndpoint_WithInvalidToken_Returns401()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/applications/mine");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "token-invalido");

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Refresh_WithValidRefreshToken_ReturnsNewTokenPair()
    {
        var login = await LoginAsAsync("admin@monburan.com");

        var response = await _client.PostAsJsonAsync("/api/auth/refresh", new RefreshTokenRequest
        {
            RefreshToken = login.RefreshToken
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<UserLoginResponse>();
        body!.AccessToken.Should().NotBeNullOrEmpty();
        body.RefreshToken.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Refresh_WithAccessToken_Returns401()
    {
        // Access token não pode ser trocado por um novo par (falta claim token_use=refresh)
        var login = await LoginAsAsync("admin@monburan.com");

        var response = await _client.PostAsJsonAsync("/api/auth/refresh", new RefreshTokenRequest
        {
            RefreshToken = login.AccessToken
        });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Refresh_WithGarbageToken_Returns401()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/refresh", new RefreshTokenRequest
        {
            RefreshToken = "lixo"
        });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Register_AsCandidate_CreatesProfileAndReturnsTokens()
    {
        var email = $"candidato.{Guid.NewGuid():N}@teste.com";

        var response = await _client.PostAsJsonAsync("/api/auth/register", new RegisterRequest
        {
            AccountType = "candidate",
            Name = "Candidato Teste",
            Email = email,
            Password = "Monburan123",
            ConfirmPassword = "Monburan123"
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var body = await response.Content.ReadFromJsonAsync<UserLoginResponse>();
        body!.AccessToken.Should().NotBeNullOrEmpty();

        // O perfil de candidato deve existir e ser acessível via /me
        using var me = new HttpRequestMessage(HttpMethod.Get, "/api/v1/candidates/me");
        me.Headers.Authorization = new AuthenticationHeaderValue("Bearer", body.AccessToken);

        var meResponse = await _client.SendAsync(me);
        meResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var candidate = await meResponse.Content.ReadFromJsonAsync<CandidateResponse>();
        candidate!.Email.Should().Be(email);
    }

    [Fact]
    public async Task Register_WithNullName_Returns400()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/register", new
        {
            accountType = "candidate",
            name = (string?)null,
            email = $"x.{Guid.NewGuid():N}@teste.com",
            password = "Monburan123",
            confirmPassword = "Monburan123"
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Register_WithInvalidEmail_Returns400()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/register", new RegisterRequest
        {
            AccountType = "candidate",
            Name = "Candidato Teste",
            Email = "nao-e-email",
            Password = "Monburan123",
            ConfirmPassword = "Monburan123"
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Register_WithMismatchedConfirmPassword_Returns400()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/register", new RegisterRequest
        {
            AccountType = "candidate",
            Name = "Candidato Teste",
            Email = $"x.{Guid.NewGuid():N}@teste.com",
            Password = "Monburan123",
            ConfirmPassword = "Outra123"
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Register_Advertiser_CannotAccessCandidateEndpoints()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/register", new RegisterRequest
        {
            AccountType = "advertiser",
            Name = "Empresa Teste",
            Email = $"adv.{Guid.NewGuid():N}@teste.com",
            Password = "Monburan123",
            ConfirmPassword = "Monburan123"
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<UserLoginResponse>();

        using var me = new HttpRequestMessage(HttpMethod.Get, "/api/v1/candidates/me");
        me.Headers.Authorization = new AuthenticationHeaderValue("Bearer", body!.AccessToken);

        var meResponse = await _client.SendAsync(me);
        meResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Login_WithDeactivatedUser_Returns401()
    {
        // Conta desativada (IsActive=false) não pode logar — senão um novo refresh
        // token de 30 dias anularia a revogação por SecurityStamp
        var email = $"desativado.{Guid.NewGuid():N}@teste.com";
        var register = await _client.PostAsJsonAsync("/api/auth/register", new RegisterRequest
        {
            AccountType = "candidate",
            Name = "Candidato Desativado",
            Email = email,
            Password = "Monburan123",
            ConfirmPassword = "Monburan123"
        });
        register.EnsureSuccessStatusCode();

        using (var scope = _factory.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider
                .GetRequiredService<Microsoft.AspNetCore.Identity.UserManager<Monburan.Web.Models.ApplicationUser>>();
            var user = await userManager.FindByEmailAsync(email);
            user!.IsActive = false;
            (await userManager.UpdateAsync(user)).Succeeded.Should().BeTrue();
        }

        var response = await _client.PostAsJsonAsync("/api/auth/login", new UserLoginRequest
        {
            Email = email,
            Password = "Monburan123"
        });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Refresh_WithDeactivatedUser_Returns401()
    {
        // Refresh também deve respeitar IsActive — caso contrário a desativação não
        // revogaria o refresh token de 30 dias
        var email = $"refresh.{Guid.NewGuid():N}@teste.com";
        var register = await _client.PostAsJsonAsync("/api/auth/register", new RegisterRequest
        {
            AccountType = "candidate",
            Name = "Candidato Refresh",
            Email = email,
            Password = "Monburan123",
            ConfirmPassword = "Monburan123"
        });
        register.EnsureSuccessStatusCode();
        var body = (await register.Content.ReadFromJsonAsync<UserLoginResponse>())!;

        using (var scope = _factory.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider
                .GetRequiredService<Microsoft.AspNetCore.Identity.UserManager<Monburan.Web.Models.ApplicationUser>>();
            var user = await userManager.FindByEmailAsync(email);
            user!.IsActive = false;
            (await userManager.UpdateAsync(user)).Succeeded.Should().BeTrue();
        }

        var response = await _client.PostAsJsonAsync("/api/auth/refresh", new RefreshTokenRequest
        {
            RefreshToken = body.RefreshToken
        });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Register_WithInvalidAccountType_Returns400()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/register", new RegisterRequest
        {
            AccountType = "hacker",
            Name = "Sem Perfil",
            Email = $"x.{Guid.NewGuid():N}@teste.com",
            Password = "Monburan123",
            ConfirmPassword = "Monburan123"
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    internal async Task<UserLoginResponse> LoginAsAsync(string email, string password = "Monburan123")
    {
        var response = await _client.PostAsJsonAsync("/api/auth/login", new UserLoginRequest
        {
            Email = email,
            Password = password
        });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<UserLoginResponse>())!;
    }
}
