using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

using FluentAssertions;

using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Monburan.Web.Data;
using Microsoft.Extensions.DependencyInjection;

using Monburan.Core.DTOs.Request;
using Monburan.Core.DTOs.Response;
using Monburan.Core.Enums;
using Monburan.Core.Interfaces.Services;
using Monburan.Web.Models;
using Monburan.Web.Services;

namespace Monburan.ApiTests;

/// <summary>M09/HI-MO: emissão revogável de token do titular e consulta mínima consentida de vagas.</summary>
public sealed class IntegrationTokensApiTests : IClassFixture<MonburanApiFactory>
{
    private const string CandidateEmail = "joao.silva@email.com";
    private const string Password = "Monburan123";
    private static readonly AdminActor Actor = new(Guid.NewGuid().ToString(), "admin@monburan.com.br");

    private readonly MonburanApiFactory _factory;
    private readonly HttpClient _client;

    public IntegrationTokensApiTests(MonburanApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public void IntegrationMigrationTargetRetainsTheJobAndApplicationSchema()
    {
        using var context = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql("Host=127.0.0.1;Database=fixture").Options);
        var migrations = context.GetService<IMigrationsAssembly>();
        var migration = migrations.CreateMigration(
            migrations.Migrations["20261126202711_IntegracaoHiMarshall"], context.Database.ProviderName!);
        var model = migration.TargetModel;

        model.FindEntityType("Monburan.Core.Entities.IntegrationToken").Should().NotBeNull();
        model.FindEntityType("Monburan.Core.Entities.JobRevision").Should().NotBeNull();
        model.FindEntityType("Monburan.Core.Entities.Job")!.FindProperty("PublicationType").Should().NotBeNull();
        model.FindEntityType("Monburan.Core.Entities.Application")!.FindProperty("ResumeSnapshotJson").Should().NotBeNull();
    }

    [Fact]
    public async Task TokenJobsRead_ReturnsOnlyPublicMinimumJobFields_AndRevocationBlocksReplay()
    {
        await EnableIntegrationFlagAsync();
        var accessToken = await LoginAsync();
        var created = await CreateTokenAsync(accessToken, ["jobs:read"]);
        created.GetProperty("token").GetString().Should().StartWith("mbi_");
        created.GetProperty("scopes").EnumerateArray().Select(scope => scope.GetString()).Should().Contain("jobs:read");

        using var jobsRequest = new HttpRequestMessage(HttpMethod.Get, "/api/v1/integrations/jobs");
        jobsRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", created.GetProperty("token").GetString());
        var jobs = await _client.SendAsync(jobsRequest);

        jobs.StatusCode.Should().Be(HttpStatusCode.OK, await jobs.Content.ReadAsStringAsync());
        var body = await jobs.Content.ReadFromJsonAsync<JsonElement>();
        var first = body.GetProperty("jobs").EnumerateArray().First();
        first.GetProperty("title").GetString().Should().NotBeNullOrWhiteSpace();
        first.GetProperty("applyUrl").GetString().Should().Contain("/jobs/");
        first.TryGetProperty("salaryMin", out _).Should().BeFalse();
        first.TryGetProperty("resume", out _).Should().BeFalse();
        first.TryGetProperty("financial", out _).Should().BeFalse();

        var revoke = await SendBearerAsync(HttpMethod.Delete, $"/api/v1/integrations/tokens/{created.GetProperty("id").GetGuid()}", accessToken);
        revoke.StatusCode.Should().Be(HttpStatusCode.NoContent, await revoke.Content.ReadAsStringAsync());

        using var replay = new HttpRequestMessage(HttpMethod.Get, "/api/v1/integrations/jobs");
        replay.Headers.Authorization = new AuthenticationHeaderValue("Bearer", created.GetProperty("token").GetString());
        (await _client.SendAsync(replay)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Jobs_WithoutTokenOrJobsScope_AreBlocked()
    {
        await EnableIntegrationFlagAsync();
        (await _client.GetAsync("/api/v1/integrations/jobs")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var accessToken = await LoginAsync();
        var readinessOnly = await CreateTokenAsync(accessToken, ["readiness:read"]);
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/integrations/jobs");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", readinessOnly.GetProperty("token").GetString());

        (await _client.SendAsync(request)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }


    [Fact]
    public async Task Jobs_DeactivatedHolder_IsBlockedEvenBeforeTokenExpiry()
    {
        await EnableIntegrationFlagAsync();
        const string maria = "maria.santos@email.com";
        var accessToken = await LoginAsync(maria);
        var created = await CreateTokenAsync(accessToken, ["jobs:read"]);

        using (var scope = _factory.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = await users.FindByEmailAsync(maria);
            user.Should().NotBeNull();
            user!.IsActive = false;
            (await users.UpdateAsync(user)).Succeeded.Should().BeTrue();
        }

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/integrations/jobs");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", created.GetProperty("token").GetString());
        (await _client.SendAsync(request)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task TokenEndpoints_RequireCandidateBearerAndRejectUnknownScopes()
    {
        await EnableIntegrationFlagAsync();
        (await _client.PostAsJsonAsync("/api/v1/integrations/tokens", new { name = "x", scopes = new[] { "jobs:read" }, expiresInDays = 30 }))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var accessToken = await LoginAsync();
        var response = await SendBearerAsync(HttpMethod.Post, "/api/v1/integrations/tokens", accessToken,
            new { name = "x", scopes = new[] { "admin:write" }, expiresInDays = 30 });
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    private async Task EnableIntegrationFlagAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var admin = scope.ServiceProvider.GetRequiredService<FeatureFlagAdminService>();
        var existing = (await admin.ListFlagsAsync()).FirstOrDefault(flag => flag.Name == "integracao-himarshall");
        var result = await admin.SaveFlagAsync(new FeatureFlagInput(existing is null ? null : existing.Id.Value, "integracao-himarshall", "Integração consentida HiMarshall", true, [], null), Actor);
        result.Status.Should().Be(EResultStatus.Success);
    }

    private async Task<string> LoginAsync(string email = CandidateEmail)
    {
        var login = await _client.PostAsJsonAsync("/api/auth/login", new UserLoginRequest { Email = email, Password = Password });
        login.StatusCode.Should().Be(HttpStatusCode.OK, await login.Content.ReadAsStringAsync());
        return (await login.Content.ReadFromJsonAsync<UserLoginResponse>())!.AccessToken!;
    }

    private async Task<JsonElement> CreateTokenAsync(string accessToken, string[] scopes)
    {
        var response = await SendBearerAsync(HttpMethod.Post, "/api/v1/integrations/tokens", accessToken,
            new { name = "teste-himarshall", scopes, expiresInDays = 30 });
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    private async Task<HttpResponseMessage> SendBearerAsync(HttpMethod method, string url, string accessToken, object? body = null)
    {
        using var request = new HttpRequestMessage(method, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        if (body is not null) request.Content = JsonContent.Create(body);
        return await _client.SendAsync(request);
    }
}
