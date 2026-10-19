using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

using FluentAssertions;

using Monburan.Core.DTOs.Request;
using Monburan.Core.DTOs.Response;

namespace Monburan.ApiTests;

/// <summary>Alertas de vaga do candidato: CRUD, validação, limite de 10 e checagem de dono.</summary>
public class JobAlertsApiTests : IClassFixture<MonburanApiFactory>
{
    private readonly HttpClient _client;

    public JobAlertsApiTests(MonburanApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task List_WithoutToken_Returns401()
    {
        var response = await _client.GetAsync("/api/v1/job-alerts");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task CreateUpdateAndDelete_Flow()
    {
        var token = await RegisterCandidateAsync();

        var create = await SendAsync(token.AccessToken, HttpMethod.Post, "/api/v1/job-alerts", new JobAlertRequest
        {
            Name = "Vagas de TI",
            Keywords = "C#, .NET",
            City = "São Paulo",
            State = "SP",
            Mode = "Remote",
            Category = "Tecnologia da informação",
            SalaryMin = 3000
        });
        create.StatusCode.Should().Be(HttpStatusCode.Created);
        var alert = await create.Content.ReadFromJsonAsync<JobAlertResponse>();
        alert!.Name.Should().Be("Vagas de TI");
        alert.State.Should().Be("SP");
        alert.IsActive.Should().BeTrue("alerta nasce ativo");

        var list = await GetAsync<List<JobAlertResponse>>(token.AccessToken, "/api/v1/job-alerts");
        list.Should().ContainSingle(item => item.Id == alert.Id);

        var update = await SendAsync(token.AccessToken, HttpMethod.Put, $"/api/v1/job-alerts/{alert.Id}", new JobAlertRequest
        {
            Name = "Vagas de TI (remoto)",
            Keywords = "C#",
            IsActive = false
        });
        update.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = await update.Content.ReadFromJsonAsync<JobAlertResponse>();
        updated!.Name.Should().Be("Vagas de TI (remoto)");
        updated.IsActive.Should().BeFalse();

        var delete = await SendAsync(token.AccessToken, HttpMethod.Delete, $"/api/v1/job-alerts/{alert.Id}");
        delete.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var after = await GetAsync<List<JobAlertResponse>>(token.AccessToken, "/api/v1/job-alerts");
        after.Should().NotContain(item => item.Id == alert.Id);
    }

    [Fact]
    public async Task Create_WithInvalidFields_Returns400()
    {
        var token = await RegisterCandidateAsync();

        var response = await SendAsync(token.AccessToken, HttpMethod.Post, "/api/v1/job-alerts", new JobAlertRequest
        {
            Name = "",
            State = "SPX",
            Mode = "Presencial",
            SalaryMin = -1
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Create_TheEleventhAlert_Returns409()
    {
        var token = await RegisterCandidateAsync();
        for (var i = 1; i <= 10; i++)
        {
            var created = await SendAsync(token.AccessToken, HttpMethod.Post, "/api/v1/job-alerts", new JobAlertRequest { Name = $"Alerta {i}" });
            created.StatusCode.Should().Be(HttpStatusCode.Created, $"o alerta {i} cabe no limite");
        }

        var extra = await SendAsync(token.AccessToken, HttpMethod.Post, "/api/v1/job-alerts", new JobAlertRequest { Name = "Alerta 11" });

        extra.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task UpdateAndDelete_FromAnotherCandidate_Return404()
    {
        var owner = await RegisterCandidateAsync();
        var stranger = await RegisterCandidateAsync();
        var create = await SendAsync(owner.AccessToken, HttpMethod.Post, "/api/v1/job-alerts", new JobAlertRequest { Name = "Alerta do dono" });
        var alert = await create.Content.ReadFromJsonAsync<JobAlertResponse>();

        var update = await SendAsync(stranger.AccessToken, HttpMethod.Put, $"/api/v1/job-alerts/{alert!.Id}", new JobAlertRequest { Name = "Roubo" });
        update.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var delete = await SendAsync(stranger.AccessToken, HttpMethod.Delete, $"/api/v1/job-alerts/{alert.Id}");
        delete.StatusCode.Should().Be(HttpStatusCode.NotFound, "alerta de outro candidato não aparece nem para excluir");
    }

    [Fact]
    public async Task Create_WithAdvertiserToken_Returns403()
    {
        var advertiser = await RegisterAdvertiserAsync();

        var response = await SendAsync(advertiser.AccessToken, HttpMethod.Post, "/api/v1/job-alerts", new JobAlertRequest { Name = "Alerta" });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    private async Task<T?> GetAsync<T>(string accessToken, string path)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        var response = await _client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<T>();
    }

    private async Task<HttpResponseMessage> SendAsync(string accessToken, HttpMethod method, string path, JobAlertRequest? body = null)
    {
        using var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        if (body is not null)
            request.Content = JsonContent.Create(body);
        return await _client.SendAsync(request);
    }

    private async Task<UserLoginResponse> RegisterCandidateAsync()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/register", new RegisterRequest
        {
            AccountType = "candidate",
            Name = "Candidato Alertas",
            Email = $"alertas.{Guid.NewGuid():N}@teste.com",
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
