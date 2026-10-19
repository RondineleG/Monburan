using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

using FluentAssertions;

using Monburan.Core.DTOs.Request;
using Monburan.Core.DTOs.Response;

namespace Monburan.ApiTests;

/// <summary>Perfil de carreira e progresso do candidato: campos vazios, persistência, validação e contagens.</summary>
public class CareerProfileProgressApiTests : IClassFixture<MonburanApiFactory>
{
    // A API escreve enums como texto (igual ao banco); o cliente de teste precisa falar a mesma língua
    private static readonly JsonSerializerOptions Json = CreateJsonOptions();

    private readonly HttpClient _client;

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }

    public CareerProfileProgressApiTests(MonburanApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetCareerProfile_WithoutProfile_Returns200WithNullFields()
    {
        var token = await RegisterCandidateAsync();

        var profile = await GetAsync<CareerProfileResponse>(token.AccessToken, "/api/v1/candidates/me/career-profile");

        profile.Should().NotBeNull();
        profile!.DesiredRole.Should().BeNull();
        profile.City.Should().BeNull();
    }

    [Fact]
    public async Task PutAndGetCareerProfile_RoundTrip()
    {
        var token = await RegisterCandidateAsync();

        var put = await SendAsync(token.AccessToken, HttpMethod.Put, "/api/v1/candidates/me/career-profile", new CareerProfileRequest
        {
            DesiredRole = "Desenvolvedor .NET",
            City = "São Paulo",
            State = "SP",
            Mode = "Remote",
            Category = "Tecnologia da informação",
            MinimumSalary = 5000,
            ContractType = Core.Enums.EContractType.Clt
        });

        put.StatusCode.Should().Be(HttpStatusCode.OK);
        var saved = await put.Content.ReadFromJsonAsync<CareerProfileResponse>(Json);
        saved!.DesiredRole.Should().Be("Desenvolvedor .NET");
        saved.ContractType.Should().Be(Core.Enums.EContractType.Clt);

        var profile = await GetAsync<CareerProfileResponse>(token.AccessToken, "/api/v1/candidates/me/career-profile");
        profile!.City.Should().Be("São Paulo");
        profile.MinimumSalary.Should().Be(5000);
    }

    [Fact]
    public async Task PutCareerProfile_WithInvalidFields_Returns400()
    {
        var token = await RegisterCandidateAsync();

        var response = await SendAsync(token.AccessToken, HttpMethod.Put, "/api/v1/candidates/me/career-profile", new CareerProfileRequest
        {
            DesiredRole = new string('x', 121),
            State = "SPX",
            Mode = "Presencial",
            MinimumSalary = -1
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Progress_WithOnlyTheName_ReportsSixItemsAndSeventeenPercent()
    {
        var token = await RegisterCandidateAsync();

        var progress = await GetAsync<CandidateProgressResponse>(token.AccessToken, "/api/v1/candidates/me/progress");

        progress!.Checklist.Should().HaveCount(6);
        progress.Checklist.Select(item => item.Key).Should().ContainInOrder("name", "phone", "resume", "careerProfile", "linkedin", "address");
        progress.Checklist.Single(item => item.Key == "name").Done.Should().BeTrue();
        progress.Checklist.Single(item => item.Key == "resume").ActionUrl.Should().Be("/resumes");
        progress.CompletenessPercent.Should().Be(17);
    }

    [Fact]
    public async Task Progress_CountsApplicationsSavedJobsAndAlerts()
    {
        var token = await RegisterCandidateAsync();
        var jobs = await _client.GetFromJsonAsync<ListJobsResponse>("/api/v1/jobs?page=1&size=1");
        var jobId = jobs!.Jobs.First().Id;

        await SendAsync(token.AccessToken, HttpMethod.Post, "/api/v1/applications", new CreateApplicationRequest { JobId = jobId });
        await SendAsync(token.AccessToken, HttpMethod.Put, $"/api/v1/jobs/{jobId}/save");
        await SendAsync(token.AccessToken, HttpMethod.Post, "/api/v1/job-alerts", new JobAlertRequest { Name = "Alerta" });

        var progress = await GetAsync<CandidateProgressResponse>(token.AccessToken, "/api/v1/candidates/me/progress");

        progress!.Applications.Total.Should().Be(1);
        progress.Applications.Sent.Should().Be(1);
        progress.SavedJobs.Should().Be(1);
        progress.ActiveAlerts.Should().Be(1);
    }

    [Fact]
    public async Task Progress_WithoutToken_Returns401()
    {
        var response = await _client.GetAsync("/api/v1/candidates/me/progress");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Progress_WithAdvertiserToken_Returns403()
    {
        var advertiser = await RegisterAdvertiserAsync();

        var response = await SendAsync(advertiser.AccessToken, HttpMethod.Get, "/api/v1/candidates/me/progress");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    private async Task<T?> GetAsync<T>(string accessToken, string path)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        var response = await _client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<T>(Json);
    }

    private async Task<HttpResponseMessage> SendAsync(string accessToken, HttpMethod method, string path)
    {
        using var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return await _client.SendAsync(request);
    }

    private async Task<HttpResponseMessage> SendAsync<T>(string accessToken, HttpMethod method, string path, T? body = default)
    {
        using var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        if (body is not null)
            request.Content = new StringContent(JsonSerializer.Serialize(body, Json), System.Text.Encoding.UTF8, "application/json");
        return await _client.SendAsync(request);
    }

    private async Task<UserLoginResponse> RegisterCandidateAsync()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/register", new RegisterRequest
        {
            AccountType = "candidate",
            Name = "Candidato Progresso",
            Email = $"progresso.{Guid.NewGuid():N}@teste.com",
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
