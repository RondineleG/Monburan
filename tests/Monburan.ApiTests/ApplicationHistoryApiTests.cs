using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

using FluentAssertions;

using Monburan.Core.DTOs.Request;
using Monburan.Core.DTOs.Response;

namespace Monburan.ApiTests;

/// <summary>Histórico de etapas da candidatura: gravado ao criar e a cada mudança, visível só aos donos.</summary>
public class ApplicationHistoryApiTests : IClassFixture<MonburanApiFactory>
{
    private readonly HttpClient _client;

    public ApplicationHistoryApiTests(MonburanApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Apply_WritesTheSentEntryInHistory()
    {
        var candidate = await RegisterCandidateAsync();
        var application = await ApplyAsync(candidate.AccessToken, await FirstJobIdAsync());

        var history = await GetAsync<List<ApplicationHistoryItemResponse>>(
            candidate.AccessToken, $"/api/v1/applications/{application.Id}/history");

        history.Should().ContainSingle();
        history![0].Status.Should().Be("Sent");
        history[0].Date.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(5));
    }

    [Fact]
    public async Task StatusChange_AddsAnEntry_VisibleToTheOwnerAdvertiser()
    {
        var advertiser = await RegisterAdvertiserAsync();
        var jobId = await CreateJobAsync(advertiser.AccessToken);
        var candidate = await RegisterCandidateAsync();
        var application = await ApplyAsync(candidate.AccessToken, jobId);

        var update = await SendAsync(advertiser.AccessToken, HttpMethod.Put, $"/api/v1/applications/{application.Id}/status",
            new UpdateApplicationStatusRequest
            {
                ApplicationId = application.Id,
                NewStatus = Core.Enums.EApplicationStatus.Viewed,
                Notes = "Currículo compatível",
                Responsible = "RH da empresa"
            });
        update.StatusCode.Should().Be(HttpStatusCode.OK);

        var history = await GetAsync<List<ApplicationHistoryItemResponse>>(
            advertiser.AccessToken, $"/api/v1/applications/{application.Id}/history");

        history.Should().HaveCount(2);
        history![0].Status.Should().Be("Sent", "a lista vai da mais antiga para a mais nova");
        history[1].Status.Should().Be("Viewed");
        history[1].Note.Should().Be("Currículo compatível");
        history[1].Responsible.Should().Be("RH da empresa");
    }

    [Fact]
    public async Task History_ForAnotherCandidateOrAdvertiser_Returns404()
    {
        var candidate = await RegisterCandidateAsync();
        var application = await ApplyAsync(candidate.AccessToken, await FirstJobIdAsync());
        var stranger = await RegisterCandidateAsync();
        var strangerAdvertiser = await RegisterAdvertiserAsync();

        var asStrangerCandidate = await SendAsync(stranger.AccessToken, HttpMethod.Get, $"/api/v1/applications/{application.Id}/history");
        asStrangerCandidate.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var asStrangerAdvertiser = await SendAsync(strangerAdvertiser.AccessToken, HttpMethod.Get, $"/api/v1/applications/{application.Id}/history");
        asStrangerAdvertiser.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task History_WithoutToken_Returns401()
    {
        var response = await _client.GetAsync($"/api/v1/applications/{Guid.NewGuid()}/history");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    private async Task<ApplicationResponse> ApplyAsync(string accessToken, Guid jobId)
    {
        var response = await SendAsync(accessToken, HttpMethod.Post, "/api/v1/applications",
            new CreateApplicationRequest { JobId = jobId });
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<ApplicationResponse>())!;
    }

    private async Task<Guid> FirstJobIdAsync()
    {
        var jobs = await _client.GetFromJsonAsync<ListJobsResponse>("/api/v1/jobs?page=1&size=1");
        return jobs!.Jobs.First().Id;
    }

    private async Task<Guid> CreateJobAsync(string accessToken)
    {
        var response = await SendAsync(accessToken, HttpMethod.Post, "/api/v1/jobs", new CreateJobRequest
        {
            Title = "Vaga com histórico",
            Description = "Descrição da vaga com histórico",
            ExpirationDate = DateTime.UtcNow.AddDays(30)
        });
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<JobResponse>())!.Id;
    }

    private async Task<T?> GetAsync<T>(string accessToken, string path)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        var response = await _client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<T>();
    }

    private async Task<HttpResponseMessage> SendAsync(string accessToken, HttpMethod method, string path)
    {
        using var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return await _client.SendAsync(request);
    }

    private async Task<HttpResponseMessage> SendAsync<T>(string accessToken, HttpMethod method, string path, T body)
    {
        using var request = new HttpRequestMessage(method, path) { Content = JsonContent.Create(body) };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return await _client.SendAsync(request);
    }

    private async Task<UserLoginResponse> RegisterCandidateAsync()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/register", new RegisterRequest
        {
            AccountType = "candidate",
            Name = "Candidato Histórico",
            Email = $"historico.{Guid.NewGuid():N}@teste.com",
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
