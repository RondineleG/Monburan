using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

using FluentAssertions;

using Microsoft.Extensions.DependencyInjection;

using Monburan.Core.Enums;
using Monburan.Core.Interfaces.Services;
using Monburan.Web.Services;

namespace Monburan.ApiTests;

public sealed class IntegrationTokenContractApiTests(MonburanApiFactory factory) : IClassFixture<MonburanApiFactory>
{
    [Fact]
    public async Task TokenInfo_ValidaEscoposSemDependerDeVagas_ERejeitaTokenRevogado()
    {
        using var scope = factory.Services.CreateScope();
        var admin = scope.ServiceProvider.GetRequiredService<FeatureFlagAdminService>();
        var actor = new AdminActor(Guid.NewGuid().ToString(), "admin@monburan.com.br");
        var flag = new FeatureFlagInput(null, "integracao-himarshall", "Integração", true, [], null);
        (await admin.SaveFlagAsync(flag, actor)).Status.Should().Be(EResultStatus.Success);

        using var client = factory.CreateClient();
        var password = $"Teste-{Guid.NewGuid():N}1!";
        using var registration = await client.PostAsJsonAsync("/api/auth/register", new
        {
            accountType = "candidate",
            name = "Candidata Integração",
            email = $"integracao-{Guid.NewGuid():N}@test.com",
            password,
            confirmPassword = password
        });
        registration.StatusCode.Should().Be(HttpStatusCode.Created, await registration.Content.ReadAsStringAsync());
        var jwt = (await registration.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("accessToken").GetString()!;

        using var issue = new HttpRequestMessage(HttpMethod.Post, "/api/v1/integrations/tokens")
        {
            Content = JsonContent.Create(new { name = "HiMarshall", scopes = new[] { "readiness:read" } })
        };
        issue.Headers.Authorization = new AuthenticationHeaderValue("Bearer", jwt);
        using var issued = await client.SendAsync(issue);
        issued.StatusCode.Should().Be(HttpStatusCode.OK, await issued.Content.ReadAsStringAsync());
        issued.Headers.CacheControl!.NoStore.Should().BeTrue();
        var body = await issued.Content.ReadFromJsonAsync<JsonElement>();
        var token = body.GetProperty("token").GetString()!;
        var id = body.GetProperty("value").GetGuid();

        using var info = await GetAsync(client, "token-info", token);
        info.StatusCode.Should().Be(HttpStatusCode.OK);
        info.Headers.CacheControl!.NoStore.Should().BeTrue();
        (await info.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("scopes")
            .EnumerateArray().Select(scope => scope.GetString()).Should().Equal("readiness:read");
        using var readiness = await GetAsync(client, "readiness-summary", token);
        readiness.StatusCode.Should().Be(HttpStatusCode.OK);

        using var jobs = await GetAsync(client, "jobs", token);
        jobs.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await jobs.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString().Should().Be("scope_missing");

        using var revoke = new HttpRequestMessage(HttpMethod.Delete, $"/api/v1/integrations/tokens/{id}");
        revoke.Headers.Authorization = new AuthenticationHeaderValue("Bearer", jwt);
        using var revoked = await client.SendAsync(revoke);
        revoked.StatusCode.Should().Be(HttpStatusCode.NoContent);

        using var revokedInfo = await GetAsync(client, "token-info", token);
        revokedInfo.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await revokedInfo.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString().Should().Be("token_invalid");
    }

    private static async Task<HttpResponseMessage> GetAsync(HttpClient client, string endpoint, string token)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/integrations/{endpoint}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await client.SendAsync(request);
    }
}
