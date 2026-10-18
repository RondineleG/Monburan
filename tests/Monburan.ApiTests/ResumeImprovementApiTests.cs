using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

using FluentAssertions;

using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using Monburan.Core.DTOs.Request;
using Monburan.Core.DTOs.Response;
using Monburan.Core.Interfaces.Services;

namespace Monburan.ApiTests;

/// <summary>Sugestões de melhoria de currículo por IA (o provedor é substituído por um cliente falso).</summary>
public class ResumeImprovementApiTests : IClassFixture<MonburanApiFactory>
{
    private const string Password = "Monburan123";
    private readonly HttpClient _client;
    private readonly FakeAiTextClient _ai;

    public ResumeImprovementApiTests(MonburanApiFactory factory)
    {
        _client = factory.CreateClient();
        _ai = factory.Services.GetRequiredService<FakeAiTextClient>();
        _ai.Reset();
    }

    [Fact]
    public async Task Availability_ReflectsWhetherTheAiIsConfigured()
    {
        var token = await LoginAsync("joao.silva@email.com");

        var on = await SendAsync(HttpMethod.Get, "/api/v1/resumes/improvements/availability", token);
        on.StatusCode.Should().Be(HttpStatusCode.OK);
        var enabled = await on.Content.ReadFromJsonAsync<JsonElement>();
        enabled.GetProperty("enabled").GetBoolean().Should().BeTrue();
        enabled.GetProperty("provider").GetString().Should().Be("Provedor de teste");

        _ai.IsConfigured = false;
        var off = await (await SendAsync(HttpMethod.Get, "/api/v1/resumes/improvements/availability", token)).Content.ReadFromJsonAsync<JsonElement>();
        off.GetProperty("enabled").GetBoolean().Should().BeFalse();
        off.GetProperty("provider").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task Improve_ReturnsSuggestionsForTheOwnResume()
    {
        var token = await LoginAsync("joao.silva@email.com");
        var resumeId = await CreateResumeAsync(token, "Analista de Dados");

        var response = await ImproveAsync(token, resumeId, consent: true);

        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        var body = await response.Content.ReadFromJsonAsync<ResumeImprovementResponse>();
        body!.Summary.Should().Be("Resumo reescrito pela IA de teste.");
        body.Experiences.Should().ContainSingle(experience => experience.Suggestions.Count == 2);
        body.SkillsToAdd.Should().Equal("Git");
        body.Model.Should().Be("modelo-de-teste");

        var sent = _ai.Requests.Should().ContainSingle().Which;
        sent.JsonObject.Should().BeTrue();
        sent.Messages[1].Content.Should().Contain("Analista de Dados").And.Contain("Fale comigo: [e-mail]");
        sent.Messages[1].Content.Should().NotContain("joao.silva@email.com");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(null)]
    public async Task Improve_RequiresExplicitConsent_AndNeverCallsTheProvider(bool? consent)
    {
        var token = await LoginAsync("joao.silva@email.com");
        var resumeId = await CreateResumeAsync(token, "Sem autorização");

        var response = await ImproveAsync(token, resumeId, consent);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).Should().Contain("autorizar");
        _ai.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task Improve_OtherCandidatesResume_IsNotFound()
    {
        var owner = await LoginAsync("joao.silva@email.com");
        var resumeId = await CreateResumeAsync(owner, "De outra pessoa");
        var intruder = await LoginAsync("maria.santos@email.com");

        var response = await ImproveAsync(intruder, resumeId, consent: true);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        _ai.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task Improve_UnknownResume_IsNotFound()
    {
        var token = await LoginAsync("joao.silva@email.com");

        var response = await ImproveAsync(token, Guid.NewGuid(), consent: true);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Improve_RequiresAuthentication()
    {
        var response = await _client.PostAsJsonAsync($"/api/v1/resumes/{Guid.NewGuid()}/improvements", new { acceptsExternalProcessing = true });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Improve_IsOnlyForCandidates()
    {
        var token = await LoginAsync("contato@techsolutions.com");

        var response = await ImproveAsync(token, Guid.NewGuid(), consent: true);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        _ai.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task Improve_WhenAiIsNotConfigured_ExplainsInsteadOfFailing()
    {
        var token = await LoginAsync("joao.silva@email.com");
        var resumeId = await CreateResumeAsync(token, "IA desligada");
        _ai.IsConfigured = false;

        var response = await ImproveAsync(token, resumeId, consent: true);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).Should().Contain("não estão ativas");
        _ai.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task Improve_WhenTheProviderFails_ShowsASafeMessage()
    {
        var token = await LoginAsync("joao.silva@email.com");
        var resumeId = await CreateResumeAsync(token, "Provedor falhando");
        _ai.Responder = _ => throw new AiClientException(AiFailureKind.ProviderError, "502 do gateway interno.example com a chave tp-segredo");

        var response = await ImproveAsync(token, resumeId, consent: true);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("Não foi possível gerar");
        body.Should().NotContain("interno.example").And.NotContain("tp-segredo");
    }

    [Fact]
    public async Task Improve_WhenTheReplyIsNotUsable_ShowsASafeMessage()
    {
        var token = await LoginAsync("joao.silva@email.com");
        var resumeId = await CreateResumeAsync(token, "Resposta ruim");
        _ai.Responder = _ => new AiCompletion("Desculpe, não consegui.", null, null, null);

        var response = await ImproveAsync(token, resumeId, consent: true);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).Should().Contain("Não foi possível gerar");
    }

    private async Task<string> LoginAsync(string email)
    {
        var response = await _client.PostAsJsonAsync("/api/auth/login", new UserLoginRequest { Email = email, Password = Password });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<UserLoginResponse>())!.AccessToken;
    }

    private async Task<Guid> CreateResumeAsync(string token, string title)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/resumes")
        {
            Content = JsonContent.Create(new CreateResumeRequest
            {
                Title = title,
                Summary = "Programo há anos. Fale comigo: joao.silva@email.com.",
                IsActive = true
            })
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await _client.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<ResumeResponse>())!.Id;
    }

    private async Task<HttpResponseMessage> ImproveAsync(string token, Guid resumeId, bool? consent)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/resumes/{resumeId}/improvements")
        {
            Content = consent is null
                ? JsonContent.Create(new { })
                : JsonContent.Create(new { acceptsExternalProcessing = consent.Value })
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await _client.SendAsync(request);
    }

    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string url, string token)
    {
        using var request = new HttpRequestMessage(method, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await _client.SendAsync(request);
    }
}

/// <summary>Mesma API, mas com o limite de usos de IA por hora reduzido para dois.</summary>
public class LowAiLimitApiFactory : MonburanApiFactory
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);

        builder.ConfigureAppConfiguration((_, configuration) =>
            configuration.AddInMemoryCollection(new Dictionary<string, string?> { ["Ai:MaxRequestsPerHour"] = "2" }));
    }
}

public class ResumeImprovementLimitApiTests : IClassFixture<LowAiLimitApiFactory>
{
    private const string Password = "Monburan123";
    private readonly HttpClient _client;
    private readonly FakeAiTextClient _ai;

    public ResumeImprovementLimitApiTests(LowAiLimitApiFactory factory)
    {
        _client = factory.CreateClient();
        _ai = factory.Services.GetRequiredService<FakeAiTextClient>();
        _ai.Reset();
    }

    [Fact]
    public async Task Improve_StopsAfterTheHourlyLimit_AndTellsWhenToTryAgain()
    {
        var login = await _client.PostAsJsonAsync("/api/auth/login", new UserLoginRequest { Email = "joao.silva@email.com", Password = Password });
        var token = (await login.Content.ReadFromJsonAsync<UserLoginResponse>())!.AccessToken;

        using var create = new HttpRequestMessage(HttpMethod.Post, "/api/v1/resumes")
        {
            Content = JsonContent.Create(new CreateResumeRequest { Title = "Limite", Summary = "Resumo", IsActive = true })
        };
        create.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var resumeId = (await (await _client.SendAsync(create)).Content.ReadFromJsonAsync<ResumeResponse>())!.Id;

        async Task<HttpResponseMessage> Improve()
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/resumes/{resumeId}/improvements")
            {
                Content = JsonContent.Create(new { acceptsExternalProcessing = true })
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            return await _client.SendAsync(request);
        }

        (await Improve()).StatusCode.Should().Be(HttpStatusCode.OK);
        (await Improve()).StatusCode.Should().Be(HttpStatusCode.OK);

        var third = await Improve();

        third.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await third.Content.ReadAsStringAsync()).Should().Contain("Tente de novo em");
        _ai.Requests.Should().HaveCount(2, "a terceira tentativa nem chega ao provedor");
    }
}
