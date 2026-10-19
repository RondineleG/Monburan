using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

using FluentAssertions;

using Monburan.Core.DTOs.Request;
using Monburan.Core.DTOs.Response;

namespace Monburan.ApiTests;

/// <summary>
/// Trilha do candidato na API: vagas salvas (idempotente), recomendações e contagem de visualizações.
/// </summary>
public class CandidateJourneyApiTests : IClassFixture<MonburanApiFactory>
{
    private readonly HttpClient _client;

    public CandidateJourneyApiTests(MonburanApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task SaveJob_IsIdempotent_AndUnsaveIsToo()
    {
        var token = await RegisterCandidateAsync();
        var jobId = await FirstJobIdAsync();

        (await SaveAsync(token.AccessToken, jobId)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await SaveAsync(token.AccessToken, jobId)).StatusCode.Should().Be(HttpStatusCode.NoContent, "salvar de novo continua 204");

        var saved = await GetAsync<ListJobsResponse>(token.AccessToken, "/api/v1/jobs/saved");
        saved!.Jobs.Should().ContainSingle(job => job.Id == jobId);
        saved.Jobs.Single().IsSaved.Should().BeTrue();

        (await UnsaveAsync(token.AccessToken, jobId)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await UnsaveAsync(token.AccessToken, jobId)).StatusCode.Should().Be(HttpStatusCode.NoContent, "remover de novo continua 204");

        var afterUnsave = await GetAsync<ListJobsResponse>(token.AccessToken, "/api/v1/jobs/saved");
        afterUnsave!.Jobs.Should().NotContain(job => job.Id == jobId);
    }

    [Fact]
    public async Task SaveJob_WithoutToken_Returns401()
    {
        var response = await _client.PutAsync($"/api/v1/jobs/{Guid.NewGuid()}/save", null);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task SaveJob_WithAdvertiserToken_Returns403()
    {
        var advertiser = await RegisterAdvertiserAsync();

        var response = await SaveAsync(advertiser.AccessToken, await FirstJobIdAsync());

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task IsSaved_IsTrueOnlyForTheCandidateWhoSaved()
    {
        var token = await RegisterCandidateAsync();
        var jobId = await FirstJobIdAsync();
        await SaveAsync(token.AccessToken, jobId);

        var forOwner = await GetAsync<JobResponse>(token.AccessToken, $"/api/v1/jobs/{jobId}");
        forOwner!.IsSaved.Should().BeTrue();

        var anonymous = await _client.GetFromJsonAsync<JobResponse>($"/api/v1/jobs/{jobId}");
        anonymous!.IsSaved.Should().BeFalse("anônimo não salva vaga");

        var otherCandidate = await RegisterCandidateAsync();
        var forOther = await GetAsync<JobResponse>(otherCandidate.AccessToken, $"/api/v1/jobs/{jobId}");
        forOther!.IsSaved.Should().BeFalse();
    }

    [Fact]
    public async Task Recommended_WithoutToken_Returns401()
    {
        var response = await _client.GetAsync("/api/v1/jobs/recommended");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Recommended_WithCareerProfile_MatchesTheProfile()
    {
        var token = await RegisterCandidateAsync();
        var jobs = await _client.GetFromJsonAsync<ListJobsResponse>("/api/v1/jobs?page=1&size=5");
        var target = jobs!.Jobs.First();
        var desiredRole = target.Title.Split(' ', StringSplitOptions.RemoveEmptyEntries).First(word => word.Length >= 3);

        var profileResponse = await PutAsync(token.AccessToken, "/api/v1/candidates/me/career-profile", new CareerProfileRequest
        {
            DesiredRole = desiredRole,
            City = target.City
        });
        profileResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var recommended = await GetAsync<ListJobsResponse>(token.AccessToken, "/api/v1/jobs/recommended");
        recommended!.Jobs.Should().Contain(job => job.Id == target.Id, "o cargo desejado aparece no título da vaga");
    }

    [Fact]
    public async Task Recommended_WithoutProfileAndSkills_ReturnsEmptyList()
    {
        var token = await RegisterCandidateAsync();

        var recommended = await GetAsync<ListJobsResponse>(token.AccessToken, "/api/v1/jobs/recommended");

        recommended!.Jobs.Should().BeEmpty("sem perfil e sem currículo não há pontuação — a UI mostra as vagas recentes");
    }

    [Fact]
    public async Task RegisterView_Anonymous_IncrementsTheCounter()
    {
        var job = await _client.GetFromJsonAsync<JobResponse>($"/api/v1/jobs/{await FirstJobIdAsync()}");
        var before = job!.Views;

        var response = await _client.PostAsync($"/api/v1/jobs/{job.Id}/views", null);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var after = await _client.GetFromJsonAsync<JobResponse>($"/api/v1/jobs/{job.Id}");
        after!.Views.Should().Be(before + 1);
    }

    [Fact]
    public async Task RegisterView_ByTheOwner_DoesNotCount()
    {
        var advertiser = await RegisterAdvertiserAsync();
        var jobId = await CreateJobAsync(advertiser.AccessToken);

        var response = await WithToken(advertiser.AccessToken, HttpMethod.Post, $"/api/v1/jobs/{jobId}/views");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var job = await GetAsync<JobResponse>(advertiser.AccessToken, $"/api/v1/jobs/{jobId}");
        job!.Views.Should().Be(0, "a visita do próprio anunciante não conta");
    }

    [Fact]
    public async Task RegisterView_UnknownJob_Returns404()
    {
        var response = await _client.PostAsync($"/api/v1/jobs/{Guid.NewGuid()}/views", null);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    private async Task<Guid> FirstJobIdAsync()
    {
        var jobs = await _client.GetFromJsonAsync<ListJobsResponse>("/api/v1/jobs?page=1&size=1");
        return jobs!.Jobs.First().Id;
    }

    private async Task<Guid> CreateJobAsync(string accessToken)
    {
        var response = await PutAsync(accessToken, "/api/v1/jobs", new CreateJobRequest
        {
            Title = "Vaga do dono",
            Description = "Descrição da vaga do dono",
            ExpirationDate = DateTime.UtcNow.AddDays(30)
        }, HttpMethod.Post);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var job = await response.Content.ReadFromJsonAsync<JobResponse>();
        return job!.Id;
    }

    private Task<HttpResponseMessage> SaveAsync(string accessToken, Guid jobId) =>
        WithToken(accessToken, HttpMethod.Put, $"/api/v1/jobs/{jobId}/save");

    private Task<HttpResponseMessage> UnsaveAsync(string accessToken, Guid jobId) =>
        WithToken(accessToken, HttpMethod.Delete, $"/api/v1/jobs/{jobId}/save");

    private async Task<T?> GetAsync<T>(string accessToken, string path)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        var response = await _client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<T>();
    }

    private async Task<HttpResponseMessage> PutAsync<T>(string accessToken, string path, T body, HttpMethod? method = null)
    {
        using var request = new HttpRequestMessage(method ?? HttpMethod.Put, path) { Content = JsonContent.Create(body) };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return await _client.SendAsync(request);
    }

    private async Task<HttpResponseMessage> WithToken(string accessToken, HttpMethod method, string path)
    {
        using var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return await _client.SendAsync(request);
    }

    private async Task<UserLoginResponse> RegisterCandidateAsync()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/register", new RegisterRequest
        {
            AccountType = "candidate",
            Name = "Candidato Trilha",
            Email = $"trilha.{Guid.NewGuid():N}@teste.com",
            Password = "Monburan123",
            ConfirmPassword = "Monburan123"
        });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<UserLoginResponse>())!;
    }

    private async Task<UserLoginResponse> RegisterAdvertiserAsync()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/register", new RegisterRequest
        {
            AccountType = "advertiser",
            Name = $"Empresa {Guid.NewGuid():N}",
            Email = $"adv.{Guid.NewGuid():N}@teste.com",
            Password = "Monburan123",
            ConfirmPassword = "Monburan123"
        });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<UserLoginResponse>())!;
    }
}
