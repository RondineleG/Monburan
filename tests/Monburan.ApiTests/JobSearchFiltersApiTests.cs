using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

using FluentAssertions;

using Monburan.Core.DTOs.Request;
using Monburan.Core.DTOs.Response;

namespace Monburan.ApiTests;

/// <summary>Filtros novos da listagem de vagas: tipo de contrato, período do salário e publicação recente.</summary>
public class JobSearchFiltersApiTests : IClassFixture<MonburanApiFactory>
{
    private readonly HttpClient _client;

    public JobSearchFiltersApiTests(MonburanApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task ListJobs_WithContractTypeAndSalaryPeriod_FiltersTheResults()
    {
        var advertiser = await RegisterAdvertiserAsync();
        var cltMonth = await CreateJobAsync(advertiser.AccessToken, "Vaga CLT mensal", Core.Enums.EContractType.Clt, Core.Enums.ESalaryPeriod.Month);
        await CreateJobAsync(advertiser.AccessToken, "Vaga PJ anual", Core.Enums.EContractType.Pj, Core.Enums.ESalaryPeriod.Year);

        var clt = await _client.GetFromJsonAsync<ListJobsResponse>("/api/v1/jobs?contractType=Clt&page=1&size=100");
        clt!.Jobs.Select(job => job.Id).Should().Contain(cltMonth);
        clt.Jobs.Should().OnlyContain(job => job.ContractType == "Clt");

        var monthly = await _client.GetFromJsonAsync<ListJobsResponse>("/api/v1/jobs?salaryPeriod=Month&page=1&size=100");
        monthly!.Jobs.Should().OnlyContain(job => job.SalaryPeriod == "Month");

        var combined = await _client.GetFromJsonAsync<ListJobsResponse>("/api/v1/jobs?contractType=Pj&salaryPeriod=Month&page=1&size=100");
        combined!.Jobs.Should().BeEmpty("os filtros novos precisam funcionar junto com os demais");
    }

    [Fact]
    public async Task ListJobs_PublishedWithinDays_KeepsOnlyRecentJobs()
    {
        var advertiser = await RegisterAdvertiserAsync();
        var jobId = await CreateJobAsync(advertiser.AccessToken, "Vaga recém-publicada", Core.Enums.EContractType.Clt, Core.Enums.ESalaryPeriod.Month);

        var response = await _client.GetFromJsonAsync<ListJobsResponse>("/api/v1/jobs?publishedWithinDays=1&page=1&size=100");

        response!.Jobs.Select(job => job.Id).Should().Contain(jobId);
    }

    [Fact]
    public async Task ListJobs_WithUnknownEnumValues_IgnoresTheFilter()
    {
        var response = await _client.GetAsync("/api/v1/jobs?contractType=NadaDisso&salaryPeriod=TambemNao");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    private async Task<Guid> CreateJobAsync(string accessToken, string title, Core.Enums.EContractType contractType, Core.Enums.ESalaryPeriod salaryPeriod)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/jobs")
        {
            Content = JsonContent.Create(new CreateJobRequest
            {
                Title = title,
                Description = "Descrição da vaga filtrável",
                ExpirationDate = DateTime.UtcNow.AddDays(30),
                SalaryMin = 3000,
                SalaryMax = 5000,
                ContractType = contractType,
                SalaryPeriod = salaryPeriod
            })
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        var response = await _client.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<JobResponse>())!.Id;
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
