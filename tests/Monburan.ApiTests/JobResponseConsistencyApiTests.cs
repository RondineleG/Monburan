using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

using FluentAssertions;

using Monburan.Core.DTOs.Request;
using Monburan.Core.DTOs.Response;

namespace Monburan.ApiTests;

/// <summary>
/// A vaga é devolvida por várias rotas (detalhe, edição, painel) e todas precisam contar a mesma história:
/// mesma empresa, mesmo logo e mesmo motivo de moderação. Divergir aqui é o que fazia o site e o app
/// desenharem cartões diferentes conforme a operação que originou a vaga.
/// </summary>
public class JobResponseConsistencyApiTests : IClassFixture<MonburanApiFactory>
{
    private const string Password = "Monburan123";
    private const string OwnerEmail = "contato@techsolutions.com";

    private readonly HttpClient _client;

    public JobResponseConsistencyApiTests(MonburanApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task TheSameJob_AnswersTheSameCompanyAndModerationReason_OnDetailOnUpdateAndOnDashboard()
    {
        var token = await TokenAsync();
        var created = await CreateJobAsync(token);

        var detail = await GetJobAsync(created.Id, token);
        var updated = await UpdateJobAsync(created.Id, token);
        var dashboard = await GetDashboardAsync(token);
        var onDashboard = dashboard.RecentJobs.Single(job => job.Id == created.Id);

        // A empresa aparece igual em todas as rotas
        detail.AdvertiserName.Should().Be("Tech Solutions Ltda");
        updated.AdvertiserName.Should().Be(detail.AdvertiserName);
        onDashboard.AdvertiserName.Should().Be(detail.AdvertiserName);
        detail.AdvertiserLogoUrl.Should().Be(updated.AdvertiserLogoUrl);
        detail.AdvertiserLogoUrl.Should().Be(onDashboard.AdvertiserLogoUrl);

        // E o motivo da moderação também (nulo aqui, porque a vaga está no ar)
        detail.ModerationReason.Should().BeNull();
        updated.ModerationReason.Should().Be(detail.ModerationReason);
        onDashboard.ModerationReason.Should().Be(detail.ModerationReason);
    }

    private async Task<string> TokenAsync()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/login", new UserLoginRequest { Email = OwnerEmail, Password = Password });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<UserLoginResponse>())!.AccessToken;
    }

    private async Task<JobResponse> CreateJobAsync(string token)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/jobs")
        {
            Content = JsonContent.Create(new CreateJobRequest
            {
                Title = "Vaga para comparar as rotas " + Guid.NewGuid().ToString("N")[..6],
                Description = "Vaga criada pelo teste de consistência da resposta.",
                ExpirationDate = DateTime.UtcNow.AddDays(20),
                NumberOfOpenings = 1,
                Type = "Remote",
                IsRemote = true,
                SalaryCurrency = "BRL",
                SalaryDisplay = true
            })
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await _client.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<JobResponse>())!;
    }

    private async Task<JobResponse> GetJobAsync(Guid id, string token)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/jobs/{id}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await _client.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<JobResponse>())!;
    }

    private async Task<JobResponse> UpdateJobAsync(Guid id, string token)
    {
        using var request = new HttpRequestMessage(HttpMethod.Put, $"/api/v1/jobs/{id}")
        {
            Content = JsonContent.Create(new UpdateJobRequest
            {
                Title = "Vaga editada " + Guid.NewGuid().ToString("N")[..6],
                Description = "Vaga editada pelo teste de consistência da resposta.",
                ExpirationDate = DateTime.UtcNow.AddDays(25),
                NumberOfOpenings = 2,
                Type = "Hybrid",
                IsRemote = true,
                Country = "BR",
                SalaryCurrency = "BRL",
                SalaryDisplay = true
            })
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await _client.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<JobResponse>())!;
    }

    private async Task<DashboardAnalyticsResponse> GetDashboardAsync(string token)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/dashboard");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await _client.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<DashboardAnalyticsResponse>())!;
    }
}
