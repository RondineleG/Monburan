using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

using FluentAssertions;

using Microsoft.AspNetCore.Mvc;

using Monburan.Core.DTOs.Request;
using Monburan.Core.DTOs.Response;

namespace Monburan.ApiTests;

/// <summary>
/// Fluxo end-to-end do candidato: registro → vaga pública → candidatura → /mine.
/// Também cobre o caso IDOR: CandidateId do body é sempre ignorado em favor da claim.
/// </summary>
public class ApplicationsApiTests : IClassFixture<MonburanApiFactory>
{
    private readonly HttpClient _client;

    public ApplicationsApiTests(MonburanApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task CandidateFlow_RegisterApplyAndListMine()
    {
        var token = await RegisterCandidateAsync();

        // pega uma vaga pública qualquer
        var jobs = await _client.GetFromJsonAsync<ListJobsResponse>("/api/v1/jobs?page=1&size=1");
        var jobId = jobs!.Jobs.First().Id;

        // tenta IDOR: manda um candidateId alheio — a API deve ignorar e usar a claim
        using var applyRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/applications")
        {
            Content = JsonContent.Create(new CreateApplicationRequest
            {
                CandidateId = Guid.NewGuid(),
                JobId = jobId,
                Notes = "Candidatura via API"
            })
        };
        applyRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);

        var applyResponse = await _client.SendAsync(applyRequest);
        applyResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var application = await applyResponse.Content.ReadFromJsonAsync<ApplicationResponse>();
        application!.JobId.Should().Be(jobId);
        application.CandidateId.Should().NotBe(Guid.Empty);

        // /mine lista a candidatura criada
        using var mineRequest = new HttpRequestMessage(HttpMethod.Get, "/api/v1/applications/mine");
        mineRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);
        var mineResponse = await _client.SendAsync(mineRequest);

        mineResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var mine = await mineResponse.Content.ReadFromJsonAsync<List<ApplicationResponse>>();
        mine.Should().ContainSingle(a => a.Id == application.Id);
        mine![0].CandidateId.Should().Be(application.CandidateId, "CandidateId deve vir da claim, não do body");
    }

    [Fact]
    public async Task Apply_WithOwnResume_PersistsResumeId()
    {
        var token = await RegisterCandidateAsync();

        var resumeId = await CreateResumeAsync(token.AccessToken);

        var jobs = await _client.GetFromJsonAsync<ListJobsResponse>("/api/v1/jobs?page=1&size=1");
        var jobId = jobs!.Jobs.First().Id;

        using var applyRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/applications")
        {
            Content = JsonContent.Create(new CreateApplicationRequest
            {
                JobId = jobId,
                ResumeId = resumeId
            })
        };
        applyRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);

        var response = await _client.SendAsync(applyRequest);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var application = await response.Content.ReadFromJsonAsync<ApplicationResponse>();
        application!.ResumeId.Should().Be(resumeId);

        // ResumeId precisa fazer round-trip no read path do próprio candidato
        using var mineRequest = new HttpRequestMessage(HttpMethod.Get, "/api/v1/applications/mine");
        mineRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);
        var mine = await (await _client.SendAsync(mineRequest)).Content.ReadFromJsonAsync<List<ApplicationResponse>>();
        mine.Should().ContainSingle(a => a.Id == application.Id)
            .Which.ResumeId.Should().Be(resumeId, "list-by-candidate deve devolver o ResumeId salvo");
    }

    [Fact]
    public async Task AdvertiserViewsAndUpdatesApplication_ResumeIdRoundTrips()
    {
        var advertiser = await RegisterAdvertiserAsync();
        var jobId = await CreateJobAsync(advertiser.AccessToken);

        var candidate = await RegisterCandidateAsync();
        var resumeId = await CreateResumeAsync(candidate.AccessToken);
        var applicationId = await ApplyAsync(candidate.AccessToken, jobId, resumeId);

        // list-by-job (anunciante dono da vaga) deve expor o ResumeId salvo
        using var listRequest = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/jobs/{jobId}/applications");
        listRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", advertiser.AccessToken);
        var byJob = await (await _client.SendAsync(listRequest)).Content.ReadFromJsonAsync<List<ApplicationResponse>>();
        byJob.Should().ContainSingle(a => a.Id == applicationId)
            .Which.ResumeId.Should().Be(resumeId, "list-by-job deve devolver o ResumeId salvo");

        // update-status (anunciante) também deve devolver ResumeId no response
        using var statusRequest = new HttpRequestMessage(HttpMethod.Put, $"/api/v1/applications/{applicationId}/status")
        {
            Content = JsonContent.Create(new { NewStatus = "Viewed" })
        };
        statusRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", advertiser.AccessToken);
        var statusResponse = await _client.SendAsync(statusRequest);
        statusResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = await statusResponse.Content.ReadFromJsonAsync<ApplicationResponse>();
        updated!.ResumeId.Should().Be(resumeId, "update-status deve devolver o ResumeId salvo");
    }

    private async Task<Guid> ApplyAsync(string accessToken, Guid jobId, Guid? resumeId = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/applications")
        {
            Content = JsonContent.Create(new CreateApplicationRequest { JobId = jobId, ResumeId = resumeId })
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        var response = await _client.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<ApplicationResponse>())!.Id;
    }

    private async Task<Guid> CreateJobAsync(string advertiserToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/jobs")
        {
            Content = JsonContent.Create(new CreateJobRequest
            {
                Title = $"Vaga {Guid.NewGuid():N}",
                Description = "Descrição da vaga de teste",
                ExpirationDate = DateTime.UtcNow.AddDays(30)
            })
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", advertiserToken);
        var response = await _client.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<JobResponse>())!.Id;
    }

    [Fact]
    public async Task Apply_WithForeignResume_Returns400()
    {
        var tokenA = await RegisterCandidateAsync();
        var resumeId = await CreateResumeAsync(tokenA.AccessToken);

        // Candidato B tenta se candidatar usando o currículo de A
        var tokenB = await RegisterCandidateAsync();
        var jobs = await _client.GetFromJsonAsync<ListJobsResponse>("/api/v1/jobs?page=1&size=1");
        var jobId = jobs!.Jobs.First().Id;

        using var applyRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/applications")
        {
            Content = JsonContent.Create(new CreateApplicationRequest
            {
                JobId = jobId,
                ResumeId = resumeId
            })
        };
        applyRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokenB.AccessToken);

        var response = await _client.SendAsync(applyRequest);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    private async Task<Guid> CreateResumeAsync(string accessToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/resumes")
        {
            Content = JsonContent.Create(new CreateResumeRequest
            {
                Title = "Currículo Teste",
                IsActive = true
            })
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var response = await _client.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var resume = await response.Content.ReadFromJsonAsync<ResumeResponse>();
        return resume!.Id;
    }

    [Fact]
    public async Task Apply_DuplicateApplication_Returns409()
    {
        var token = await RegisterCandidateAsync();
        var jobs = await _client.GetFromJsonAsync<ListJobsResponse>("/api/v1/jobs?page=1&size=1");
        var jobId = jobs!.Jobs.First().Id;

        async Task<HttpResponseMessage> ApplyAsync()
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, "/api/v1/applications")
            {
                Content = JsonContent.Create(new CreateApplicationRequest { JobId = jobId })
            };
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);
            return await _client.SendAsync(req);
        }

        (await ApplyAsync()).StatusCode.Should().Be(HttpStatusCode.Created);
        var second = await ApplyAsync();
        second.StatusCode.Should().Be(HttpStatusCode.Conflict,
            "segunda candidatura na mesma vaga deve ser 409, não 400 genérico");

        var problem = await second.Content.ReadFromJsonAsync<ProblemDetails>();
        problem!.Detail.Should().Contain("candidatou",
            "o detail deve trazer a descrição de domínio, não o título genérico");
    }

    [Fact]
    public async Task Apply_UnknownJob_Returns404()
    {
        var token = await RegisterCandidateAsync();

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/applications")
        {
            Content = JsonContent.Create(new CreateApplicationRequest { JobId = Guid.NewGuid() })
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        problem!.Detail.Should().Contain("Vaga não encontrada",
            "404 deve trazer a descrição de domínio, não o título genérico");
    }

    [Fact]
    public async Task Apply_WithoutToken_Returns401()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/applications", new CreateApplicationRequest
        {
            CandidateId = Guid.NewGuid(),
            JobId = Guid.NewGuid()
        });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task AdvertiserToken_CannotApplyAsCandidate_Returns403()
    {
        // registra anunciante e tenta aplicar como candidato
        var email = $"anunciante.{Guid.NewGuid():N}@teste.com";
        var register = await _client.PostAsJsonAsync("/api/auth/register", new RegisterRequest
        {
            AccountType = "advertiser",
            Name = "Empresa XYZ",
            Email = email,
            Password = "Monburan123",
            ConfirmPassword = "Monburan123"
        });
        register.EnsureSuccessStatusCode();
        var token = (await register.Content.ReadFromJsonAsync<UserLoginResponse>())!;

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/applications")
        {
            Content = JsonContent.Create(new CreateApplicationRequest { JobId = Guid.NewGuid() })
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);

        var response = await _client.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
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

    private async Task<UserLoginResponse> RegisterCandidateAsync()
    {
        var email = $"candidato.{Guid.NewGuid():N}@teste.com";
        var response = await _client.PostAsJsonAsync("/api/auth/register", new RegisterRequest
        {
            AccountType = "candidate",
            Name = "Candidato Flow",
            Email = email,
            Password = "Monburan123",
            ConfirmPassword = "Monburan123"
        });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<UserLoginResponse>())!;
    }
}
