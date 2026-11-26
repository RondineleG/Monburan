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
using Monburan.Core.UseCases.Jobs;
using Monburan.Core.ValueObjects;
using Monburan.Web.Data;

namespace Monburan.ApiTests;

public sealed class JobDraftApiTests : IClassFixture<MonburanApiFactory>
{
    private readonly MonburanApiFactory _factory;
    private readonly HttpClient _client;

    public JobDraftApiTests(MonburanApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Draft_IsPrivateUntilPublishedThroughTheApi()
    {
        await EnableFeatureForRecruitersAsync(CreateJobUseCase.DraftJobFeature);
        await EnableFeatureForRecruitersAsync(ChangeJobStatusUseCase.ApiStatusFeature);
        var advertiser = await RegisterAdvertiserAsync();
        var title = $"Rascunho API {Guid.NewGuid():N}";

        var created = await SendAsync(HttpMethod.Post, "/api/v1/jobs", advertiser.AccessToken, new CreateJobRequest
        {
            Title = title,
            Description = "Descrição completa da vaga de teste",
            ExpirationDate = DateTime.UtcNow.AddDays(30),
            SaveAsDraft = true
        });
        created.StatusCode.Should().Be(HttpStatusCode.Created);
        var job = await created.Content.ReadFromJsonAsync<JobResponse>();
        job.Should().NotBeNull();
        job!.Status.Should().Be("Draft");

        var publicList = await _client.GetFromJsonAsync<ListJobsResponse>($"/api/v1/jobs?searchTerm={Uri.EscapeDataString(title)}");
        publicList!.Jobs.Should().BeEmpty("rascunho não entra na busca pública");

        var mine = await ReadAsync<ListJobsResponse>(HttpMethod.Get, "/api/v1/jobs/mine?size=100", advertiser.AccessToken);
        mine!.Jobs.Should().ContainSingle(item => item.Id == job.Id && item.Status == "Draft");

        var published = await SendAsync(HttpMethod.Put, $"/api/v1/jobs/{job.Id}/status", advertiser.AccessToken, new { newStatus = "Active" });
        published.StatusCode.Should().Be(HttpStatusCode.OK);
        var publishedJob = await published.Content.ReadFromJsonAsync<JobResponse>();
        publishedJob!.Status.Should().Be("Active");

        var publicAfter = await _client.GetFromJsonAsync<ListJobsResponse>($"/api/v1/jobs?searchTerm={Uri.EscapeDataString(title)}");
        publicAfter!.Jobs.Should().ContainSingle(item => item.Id == job.Id);
    }

    private async Task EnableFeatureForRecruitersAsync(string feature)
    {
        await using var context = await _factory.Services
            .GetRequiredService<IDbContextFactory<ApplicationDbContext>>()
            .CreateDbContextAsync();
        var flag = await context.FeatureFlags.SingleOrDefaultAsync(item => item.Name == feature);
        if (flag is null)
        {
            context.FeatureFlags.Add(new FeatureFlag
            {
                Id = Id.Create(Guid.NewGuid()),
                Name = feature,
                Description = "Guardar vaga como rascunho antes de publicar",
                EnabledForEveryone = false,
                Groups = "recrutador",
                TenantId = "default",
                CreatedBy = "teste",
                UpdatedBy = string.Empty,
                DeletedBy = string.Empty
            });
        }
        else
        {
            flag.EnabledForEveryone = false;
            flag.Groups = "recrutador";
        }

        await context.SaveChangesAsync();
        _factory.Services.GetRequiredService<IFeatureFlags>().Invalidate();
    }

    private async Task<UserLoginResponse> RegisterAdvertiserAsync()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/register", new RegisterRequest
        {
            AccountType = "advertiser",
            Name = "Empresa Rascunho",
            Email = $"rascunho.{Guid.NewGuid():N}@teste.com",
            Password = "Monburan123",
            ConfirmPassword = "Monburan123"
        });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<UserLoginResponse>())!;
    }

    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string path, string accessToken, object body)
    {
        using var request = new HttpRequestMessage(method, path)
        {
            Content = JsonContent.Create(body)
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return await _client.SendAsync(request);
    }

    private async Task<T?> ReadAsync<T>(HttpMethod method, string path, string accessToken)
    {
        using var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        var response = await _client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<T>();
    }
}
