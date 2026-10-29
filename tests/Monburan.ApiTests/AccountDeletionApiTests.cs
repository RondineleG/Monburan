using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

using FluentAssertions;

using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

using Monburan.Core.DTOs.Request;
using Monburan.Core.DTOs.Response;
using Monburan.Web.Models;

namespace Monburan.ApiTests;

/// <summary>Exclusão da conta pelo app (exigência das lojas): mesma regra da página /conta/excluir-dados.</summary>
public sealed class AccountDeletionApiTests : IClassFixture<MonburanApiFactory>
{
    private const string Password = "Monburan123";

    private readonly HttpClient _client;
    private readonly MonburanApiFactory _factory;

    public AccountDeletionApiTests(MonburanApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private async Task<(string Email, UserLoginResponse Tokens)> RegisterCandidateAsync()
    {
        var email = $"exclusao.{Guid.NewGuid():N}@teste.com";
        var response = await _client.PostAsJsonAsync("/api/auth/register", new RegisterRequest
        {
            AccountType = "candidate",
            Name = "Candidata Exclusão",
            Email = email,
            Password = Password,
            ConfirmPassword = Password
        });
        response.EnsureSuccessStatusCode();
        return (email, (await response.Content.ReadFromJsonAsync<UserLoginResponse>())!);
    }

    private Task<HttpResponseMessage> DeleteAsync(string? accessToken, string password)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/account/delete")
        {
            Content = JsonContent.Create(new DeleteAccountRequest { Password = password })
        };
        if (accessToken is not null)
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return _client.SendAsync(request);
    }

    [Fact]
    public async Task Delete_WithoutToken_Returns401()
    {
        var response = await DeleteAsync(null, Password);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Delete_WithWrongPassword_Returns400_AndKeepsTheAccount()
    {
        var (email, tokens) = await RegisterCandidateAsync();

        var response = await DeleteAsync(tokens.AccessToken, "senha-errada");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        using var scope = _factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        (await users.FindByEmailAsync(email)).Should().NotBeNull();
    }

    [Fact]
    public async Task Delete_WithPassword_RemovesTheUser_AndTheSessionStopsWorking()
    {
        var (email, tokens) = await RegisterCandidateAsync();

        var response = await DeleteAsync(tokens.AccessToken, Password);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        using (var scope = _factory.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            (await users.FindByEmailAsync(email)).Should().BeNull();
        }

        var login = await _client.PostAsJsonAsync("/api/auth/login", new UserLoginRequest { Email = email, Password = Password });
        login.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var refresh = await _client.PostAsJsonAsync("/api/auth/refresh", new RefreshTokenRequest { RefreshToken = tokens.RefreshToken! });
        refresh.IsSuccessStatusCode.Should().BeFalse();
    }
}
