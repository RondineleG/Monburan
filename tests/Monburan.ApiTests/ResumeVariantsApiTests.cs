using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

using FluentAssertions;

using Microsoft.Extensions.DependencyInjection;

using Monburan.Core.DTOs.Request;
using Monburan.Core.DTOs.Response;
using Monburan.Core.Enums;
using Monburan.Core.Interfaces.Services;
using Monburan.Web.Services;

namespace Monburan.ApiTests;

/// <summary>
/// Variantes localizadas do currículo (PRD carreira internacional, P0 es-ES): criar, editar, pré-visualizar e excluir sem
/// tocar o currículo-base; sugestões de IA só entram com aceite explícito; atrás da flag carreira-internacional.
/// </summary>
public sealed class ResumeVariantsApiTests : IClassFixture<MonburanApiFactory>
{
    private const string Password = "Monburan123";
    private const string Feature = "carreira-internacional";
    private static readonly AdminActor Actor = new(Guid.NewGuid().ToString(), "admin@monburan.com.br");

    private readonly MonburanApiFactory _factory;
    private readonly HttpClient _client;
    private readonly FakeAiTextClient _ai;

    public ResumeVariantsApiTests(MonburanApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
        _ai = factory.Services.GetRequiredService<FakeAiTextClient>();
        _ai.Reset();
    }

    private async Task EnableFeatureAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var admin = scope.ServiceProvider.GetRequiredService<FeatureFlagAdminService>();
        if ((await admin.ListFlagsAsync()).All(flag => flag.Name != Feature))
            (await admin.SaveFlagAsync(new FeatureFlagInput(null, Feature, "Carreira internacional", true, [], null), Actor))
                .Status.Should().Be(EResultStatus.Success);
    }

    private async Task<string> LoginAsync(string email)
    {
        var response = await _client.PostAsJsonAsync("/api/auth/login", new UserLoginRequest { Email = email, Password = Password });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<UserLoginResponse>())!.AccessToken;
    }

    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string url, string token, object? body = null)
    {
        using var request = new HttpRequestMessage(method, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (body is not null)
            request.Content = JsonContent.Create(body);
        return await _client.SendAsync(request);
    }

    private async Task<Guid> CreateResumeAsync(string token, string title)
    {
        var response = await SendAsync(HttpMethod.Post, "/api/v1/resumes", token, new CreateResumeRequest
        {
            Title = title,
            Summary = "Desenvolvedora com cinco anos de experiência.",
            Skills = [new SkillRequest { Skill = "Atendimento ao cliente", Level = 3, YearsOfExperience = 2 }]
        });
        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }

    private async Task<ResumeVariantResponse> CreateVariantAsync(string token, Guid resumeId, string locale = "es-ES")
    {
        var response = await SendAsync(HttpMethod.Post, $"/api/v1/resumes/{resumeId}/variants", token, new ResumeVariantCreateRequest { Locale = locale });
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<ResumeVariantResponse>())!;
    }

    [Fact]
    public async Task EditingTheVariant_NeverChangesTheBaseResume()
    {
        await EnableFeatureAsync();
        var token = await LoginAsync("joao.silva@email.com");
        var resumeId = await CreateResumeAsync(token, "Atendente de loja");
        var variant = await CreateVariantAsync(token, resumeId);

        var update = await SendAsync(HttpMethod.Put, $"/api/v1/resumes/variants/{variant.Id}", token, new ResumeVariantUpdateRequest
        {
            Overrides = new() { ["title"] = "Dependienta de tienda", ["skills:0"] = "Atención al cliente" }
        });
        update.StatusCode.Should().Be(HttpStatusCode.OK, await update.Content.ReadAsStringAsync());

        var preview = await (await SendAsync(HttpMethod.Get, $"/api/v1/resumes/variants/{variant.Id}/preview", token))
            .Content.ReadFromJsonAsync<ResumeVariantPreviewResponse>();
        preview!.Title.Should().Be("Dependienta de tienda");
        preview.Skills.Should().Equal("Atención al cliente");
        preview.Summary.Should().Be("Desenvolvedora com cinco anos de experiência.", "sem tradução, a variante mostra o texto da base");

        var baseResume = await (await SendAsync(HttpMethod.Get, $"/api/v1/resumes/{resumeId}", token)).Content.ReadFromJsonAsync<JsonElement>();
        baseResume.GetProperty("title").GetString().Should().Be("Atendente de loja");
    }

    [Fact]
    public async Task AiSuggestions_StayPendingUntilAccepted_AndUnknownKeysAreDropped()
    {
        await EnableFeatureAsync();
        var token = await LoginAsync("joao.silva@email.com");
        var resumeId = await CreateResumeAsync(token, "Atendente");
        var variant = await CreateVariantAsync(token, resumeId);
        _ai.Responder = _ => new AiCompletion("""{"title":"Dependiente","certificacion_inventada":"DELE C2"}""", "modelo-de-teste", 1, 1);

        var generated = await SendAsync(HttpMethod.Post, $"/api/v1/resumes/variants/{variant.Id}/suggestions", token);
        generated.StatusCode.Should().Be(HttpStatusCode.OK, await generated.Content.ReadAsStringAsync());
        var pending = await generated.Content.ReadFromJsonAsync<ResumeVariantResponse>();
        pending!.HasPendingSuggestions.Should().BeTrue();
        pending.Overrides.Should().BeEmpty("nada entra na variante sem a confirmação da pessoa");

        var preview = await (await SendAsync(HttpMethod.Get, $"/api/v1/resumes/variants/{variant.Id}/preview", token))
            .Content.ReadFromJsonAsync<ResumeVariantPreviewResponse>();
        preview!.Title.Should().Be("Atendente");
        preview.PendingSuggestions.Should().ContainKey("title").And.NotContainKey("certificacion_inventada");

        var accepted = await SendAsync(HttpMethod.Post, $"/api/v1/resumes/variants/{variant.Id}/suggestions/resolve", token,
            new ResumeVariantResolveSuggestionsRequest { Accept = true });
        accepted.StatusCode.Should().Be(HttpStatusCode.OK, await accepted.Content.ReadAsStringAsync());
        (await accepted.Content.ReadFromJsonAsync<ResumeVariantResponse>())!.Overrides.Should().Equal(new Dictionary<string, string> { ["title"] = "Dependiente" });
    }

    [Fact]
    public async Task DeletedVariant_CanBeCreatedAgainForTheSameLocale()
    {
        await EnableFeatureAsync();
        var token = await LoginAsync("joao.silva@email.com");
        var resumeId = await CreateResumeAsync(token, "Recriar variante");
        var first = await CreateVariantAsync(token, resumeId);

        (await SendAsync(HttpMethod.Delete, $"/api/v1/resumes/variants/{first.Id}", token)).StatusCode.Should().Be(HttpStatusCode.OK);

        var again = await CreateVariantAsync(token, resumeId);
        again.Id.Should().NotBe(first.Id);
    }

    [Fact]
    public async Task DuplicateLocale_AndInvalidLocale_AreRejected()
    {
        await EnableFeatureAsync();
        var token = await LoginAsync("joao.silva@email.com");
        var resumeId = await CreateResumeAsync(token, "Locale repetido");
        await CreateVariantAsync(token, resumeId);

        (await SendAsync(HttpMethod.Post, $"/api/v1/resumes/{resumeId}/variants", token, new ResumeVariantCreateRequest { Locale = "es-ES" }))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await SendAsync(HttpMethod.Post, $"/api/v1/resumes/{resumeId}/variants", token, new ResumeVariantCreateRequest { Locale = "espanhol" }))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task SomeoneElsesVariant_IsNotFound()
    {
        await EnableFeatureAsync();
        var owner = await LoginAsync("joao.silva@email.com");
        var resumeId = await CreateResumeAsync(owner, "Variante de outra pessoa");
        var variant = await CreateVariantAsync(owner, resumeId);
        var intruder = await LoginAsync("maria.santos@email.com");

        (await SendAsync(HttpMethod.Get, $"/api/v1/resumes/variants/{variant.Id}/preview", intruder)).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await SendAsync(HttpMethod.Get, $"/api/v1/resumes/{resumeId}/variants", intruder)).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await SendAsync(HttpMethod.Delete, $"/api/v1/resumes/variants/{variant.Id}", intruder)).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
