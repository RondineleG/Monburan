using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using FluentAssertions;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using Monburan.Core.DTOs.Request;

namespace Monburan.ApiTests;

/// <summary>Faz a conexão parecer vir do nginx (ponte do Docker), com o IP do cliente em X-Forwarded-For.</summary>
public sealed class ProxyPeerStartupFilter : IStartupFilter
{
    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
    {
        app.Use((context, nextMiddleware) =>
        {
            context.Connection.RemoteIpAddress = IPAddress.Parse("172.18.0.1");
            return nextMiddleware(context);
        });
        next(app);
    };
}

/// <summary>A mesma API com limites baixos: 5 envios de login por minuto por IP e muita folga no balde geral.</summary>
public class AuthLimitedApiFactory : MonburanApiFactory
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);

        builder.ConfigureAppConfiguration((_, configuration) =>
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Security:RateLimit:AuthRequestsPerMinutePerIp"] = "5",
                ["Security:RateLimit:RequestsPerSecondPerIp"] = "1000"
            }));
        builder.ConfigureServices(services => services.AddSingleton<IStartupFilter, ProxyPeerStartupFilter>());
    }
}

/// <summary>A mesma API com o balde geral minúsculo: 2 requisições por segundo por IP (rajada de 4).</summary>
public class GeneralLimitedApiFactory : MonburanApiFactory
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);

        builder.ConfigureAppConfiguration((_, configuration) =>
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Security:RateLimit:AuthRequestsPerMinutePerIp"] = "1000",
                ["Security:RateLimit:RequestsPerSecondPerIp"] = "2"
            }));
        builder.ConfigureServices(services => services.AddSingleton<IStartupFilter, ProxyPeerStartupFilter>());
    }
}

public class AuthRateLimitApiTests : IClassFixture<AuthLimitedApiFactory>
{
    private readonly HttpClient _client;

    public AuthRateLimitApiTests(AuthLimitedApiFactory factory) => _client = factory.CreateClient();

    private async Task<HttpResponseMessage> LoginAsync(string clientIp)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/login")
        {
            // E-mail que não existe: não conta para o bloqueio de nenhuma conta
            Content = JsonContent.Create(new UserLoginRequest { Email = $"ninguem.{Guid.NewGuid():N}@teste.com", Password = "Cavalo-Azul-Bateria-7" })
        };
        request.Headers.Add("X-Forwarded-For", clientIp);
        return await _client.SendAsync(request);
    }

    [Fact]
    public async Task Login_IsLimitedPerIp_AndTheRefusalExplainsWhenToTryAgain()
    {
        for (var attempt = 1; attempt <= 5; attempt++)
            (await LoginAsync("198.51.100.10")).StatusCode.Should().Be(HttpStatusCode.Unauthorized, $"tentativa {attempt}");

        var refused = await LoginAsync("198.51.100.10");

        refused.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        refused.Headers.RetryAfter!.Delta.Should().BeGreaterThan(TimeSpan.Zero);
        var problem = await refused.Content.ReadFromJsonAsync<JsonElement>();
        problem.GetProperty("status").GetInt32().Should().Be(429);
        problem.GetProperty("detail").GetString().Should().Contain("Muitas tentativas");
    }

    [Fact]
    public async Task TheLimitIsPerClient_AnotherIpIsNotAffected()
    {
        for (var attempt = 1; attempt <= 6; attempt++)
            await LoginAsync("198.51.100.20");
        (await LoginAsync("198.51.100.20")).StatusCode.Should().Be(HttpStatusCode.TooManyRequests);

        (await LoginAsync("198.51.100.21")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task RegistrationAndPasswordRecoveryShareTheSameBudget()
    {
        const string ip = "198.51.100.30";
        for (var attempt = 1; attempt <= 3; attempt++)
            await LoginAsync(ip);

        for (var attempt = 1; attempt <= 2; attempt++)
        {
            using var forgot = new HttpRequestMessage(HttpMethod.Post, "/api/auth/forgot-password")
            {
                Content = JsonContent.Create(new { email = "ninguem@teste.com" })
            };
            forgot.Headers.Add("X-Forwarded-For", ip);
            (await _client.SendAsync(forgot)).StatusCode.Should().NotBe(HttpStatusCode.TooManyRequests);
        }

        using var register = new HttpRequestMessage(HttpMethod.Post, "/api/auth/register")
        {
            Content = JsonContent.Create(new RegisterRequest { AccountType = "candidate", Name = "A", Email = "x", Password = "1", ConfirmPassword = "2" })
        };
        register.Headers.Add("X-Forwarded-For", ip);
        (await _client.SendAsync(register)).StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
    }

    [Fact]
    public async Task ReadingPublicDataIsNotCountedAgainstTheLoginBudget()
    {
        const string ip = "198.51.100.40";
        for (var attempt = 1; attempt <= 6; attempt++)
            await LoginAsync(ip);

        using var jobs = new HttpRequestMessage(HttpMethod.Get, "/api/v1/jobs");
        jobs.Headers.Add("X-Forwarded-For", ip);
        (await _client.SendAsync(jobs)).StatusCode.Should().Be(HttpStatusCode.OK);

        using var page = new HttpRequestMessage(HttpMethod.Get, "/Account/Login");
        page.Headers.Add("X-Forwarded-For", ip);
        (await _client.SendAsync(page)).StatusCode.Should().Be(HttpStatusCode.OK);
    }
}

public class GeneralRateLimitApiTests : IClassFixture<GeneralLimitedApiFactory>
{
    private readonly HttpClient _client;

    public GeneralRateLimitApiTests(GeneralLimitedApiFactory factory) => _client = factory.CreateClient();

    private Task<HttpResponseMessage> GetAsync(string path, string clientIp)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Add("X-Forwarded-For", clientIp);
        return _client.SendAsync(request);
    }

    [Fact]
    public async Task AnIpThatHammersTheSite_IsThrottled_WhileAnotherKeepsBeingServed()
    {
        var statuses = await Task.WhenAll(Enumerable.Range(0, 30).Select(_ => GetAsync("/api/v1/jobs", "198.51.100.50")));

        statuses.Count(response => response.StatusCode == HttpStatusCode.OK).Should().BeGreaterThan(0);
        statuses.Count(response => response.StatusCode == HttpStatusCode.TooManyRequests).Should().BeGreaterThan(0);

        (await GetAsync("/api/v1/jobs", "198.51.100.51")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Health_IsNeverThrottled_SoMonitoringKeepsWorking()
    {
        var statuses = await Task.WhenAll(Enumerable.Range(0, 30).Select(_ => GetAsync("/health", "198.51.100.60")));

        statuses.Should().OnlyContain(response => response.StatusCode == HttpStatusCode.OK);
    }
}
