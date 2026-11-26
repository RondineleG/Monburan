using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Monburan.Core.DTOs.Request;
using Monburan.Core.DTOs.Response;
using Monburan.Core.Entities;
using Monburan.Core.Interfaces.Services;
using Monburan.Core.UseCases.Applications;
using Monburan.Core.ValueObjects;
using Monburan.Web.Data;

namespace Monburan.ApiTests;

public sealed class ApplicationSnapshotFeatureApiTests : IClassFixture<MonburanApiFactory>
{
    private readonly MonburanApiFactory _factory;
    private readonly HttpClient _client;

    public ApplicationSnapshotFeatureApiTests(MonburanApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Apply_WhenSnapshotFeatureEnabled_RecruiterReadsOriginalAfterResumeEdit()
    {
        var advertiser = await RegisterAsync("advertiser");
        var jobId = await CreateJobAsync(advertiser.AccessToken);

        await SetSnapshotFeatureGroupsAsync("recrutador");
        var outsideCandidate = await RegisterAsync("candidate");
        var outsideResumeId = await CreateResumeAsync(outsideCandidate.AccessToken);
        var outsideApplicationId = await ApplyAsync(outsideCandidate.AccessToken, jobId, outsideResumeId);

        await SetSnapshotFeatureGroupsAsync("candidato");
        var candidate = await RegisterAsync("candidate");
        var resumeId = await CreateResumeAsync(candidate.AccessToken);

        var applicationId = await ApplyAsync(candidate.AccessToken, jobId, resumeId);
        await UpdateResumeAsync(candidate.AccessToken, resumeId);

        using var listRequest = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/jobs/{jobId}/applications");
        listRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", advertiser.AccessToken);
        var response = await _client.SendAsync(listRequest);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var applications = await response.Content.ReadFromJsonAsync<List<ApplicationResponse>>();

        applications.Should().HaveCount(2);
        applications.Single(a => a.Id == outsideApplicationId).ResumeSnapshot.Should().BeNull(
            "candidato fora do grupo da flag não deve capturar snapshot");

        var application = applications.Single(a => a.Id == applicationId);
        application.ResumeId.Should().Be(resumeId);
        application.ResumeSnapshot.Should().NotBeNull();
        application.ResumeSnapshot!.Title.Should().Be("Currículo original");
        application.ResumeSnapshot.Summary.Should().Be("Resumo original");
    }

    private async Task SetSnapshotFeatureGroupsAsync(string groups)
    {
        await using var context = await _factory.Services
            .GetRequiredService<IDbContextFactory<ApplicationDbContext>>()
            .CreateDbContextAsync();
        var flag = await context.FeatureFlags.SingleOrDefaultAsync(f => f.Name == CreateApplicationUseCase.ResumeSnapshotFeature);
        if (flag is null)
        {
            context.FeatureFlags.Add(new FeatureFlag
            {
                Id = Id.Create(Guid.NewGuid()),
                Name = CreateApplicationUseCase.ResumeSnapshotFeature,
                Description = "Guardar fotografia do currículo enviado na candidatura",
                EnabledForEveryone = false,
                Groups = groups,
                TenantId = "default",
                CreatedBy = "teste",
                DeletedBy = string.Empty,
                UpdatedBy = string.Empty
            });
        }
        else
        {
            flag.EnabledForEveryone = false;
            flag.Groups = groups;
        }

        await context.SaveChangesAsync();

        _factory.Services.GetRequiredService<IFeatureFlags>().Invalidate();
    }

    private async Task<UserLoginResponse> RegisterAsync(string accountType)
    {
        var response = await _client.PostAsJsonAsync("/api/auth/register", new RegisterRequest
        {
            AccountType = accountType,
            Name = accountType == "candidate" ? "Candidato Snapshot" : "Empresa Snapshot",
            Email = $"{accountType}.{Guid.NewGuid():N}@teste.com",
            Password = "Monburan123",
            ConfirmPassword = "Monburan123"
        });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<UserLoginResponse>())!;
    }

    private async Task<Guid> CreateJobAsync(string accessToken)
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
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        var response = await _client.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<JobResponse>())!.Id;
    }

    private async Task<Guid> CreateResumeAsync(string accessToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/resumes")
        {
            Content = JsonContent.Create(new CreateResumeRequest
            {
                Title = "Currículo original",
                Summary = "Resumo original",
                IsActive = true
            })
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        var response = await _client.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<ResumeResponse>())!.Id;
    }

    private async Task<Guid> ApplyAsync(string accessToken, Guid jobId, Guid resumeId)
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

    private async Task UpdateResumeAsync(string accessToken, Guid resumeId)
    {
        using var request = new HttpRequestMessage(HttpMethod.Put, $"/api/v1/resumes/{resumeId}")
        {
            Content = JsonContent.Create(new UpdateResumeRequest
            {
                Title = "Currículo editado depois",
                Summary = "Resumo editado depois",
                IsActive = true
            })
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        var response = await _client.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
