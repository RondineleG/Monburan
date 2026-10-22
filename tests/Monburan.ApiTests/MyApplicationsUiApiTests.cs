using System.Net;

using FluentAssertions;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

using Monburan.Core.Entities;
using Monburan.Core.Interfaces.Repositories;
using Monburan.Core.Requests;
using Monburan.Web.Data;
using Monburan.Web.Repositories;

namespace Monburan.ApiTests;

public class MyApplicationsUiApiTests
{
    [Theory]
    [InlineData("candidaturas", "salvas", "suas candidaturas")]
    [InlineData("salvas", "alertas", "suas vagas salvas")]
    [InlineData("alertas", "candidaturas", "seus alertas")]
    public async Task FailureInOneTab_DoesNotBlockTheOthers(string failedTab, string availableTab, string errorText)
    {
        await using var factory = new FailingTabFactory(failedTab);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true
        });
        await CookieAuth.LoginAsync(client, "joao.silva@email.com", "Monburan123");

        var failed = await client.GetAsync($"/my-applications?aba={failedTab}");
        var failedHtml = WebUtility.HtmlDecode(await failed.Content.ReadAsStringAsync());
        failed.StatusCode.Should().Be(HttpStatusCode.OK);
        failedHtml.Should().Contain($"Não foi possível carregar {errorText}")
            .And.Contain("data-testid=\"error-retry\"");
        failedHtml.Should().NotContain("data-testid=\"applications-empty\"");

        var available = await client.GetAsync($"/my-applications?aba={availableTab}");
        var availableHtml = WebUtility.HtmlDecode(await available.Content.ReadAsStringAsync());
        available.StatusCode.Should().Be(HttpStatusCode.OK);
        availableHtml.Should().NotContain("data-testid=\"error-state\"");
    }

    private sealed class FailingTabFactory(string failedTab) : MonburanApiFactory
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.ConfigureTestServices(services =>
            {
                switch (failedTab)
                {
                    case "candidaturas":
                        services.RemoveAll<IApplicationRepository>();
                        services.AddScoped<IApplicationRepository>(provider => new FailingApplicationsRepository(
                            provider.GetRequiredService<IDbContextFactory<ApplicationDbContext>>()));
                        break;
                    case "salvas":
                        services.RemoveAll<ISavedJobRepository>();
                        services.AddScoped<ISavedJobRepository>(provider => new FailingSavedJobsRepository(
                            provider.GetRequiredService<IDbContextFactory<ApplicationDbContext>>()));
                        break;
                    case "alertas":
                        services.RemoveAll<IJobAlertRepository>();
                        services.AddScoped<IJobAlertRepository>(provider => new FailingAlertsRepository(
                            provider.GetRequiredService<IDbContextFactory<ApplicationDbContext>>()));
                        break;
                }
            });
        }
    }

    private sealed class FailingApplicationsRepository(IDbContextFactory<ApplicationDbContext> db)
        : ApplicationRepository(db), IApplicationRepository
    {
        Task<RequestResult<IEnumerable<Application>>> IApplicationRepository.ListByCandidateAsync(
            Guid candidateId, int page, int size, CancellationToken cancellationToken) =>
            Task.FromResult(RequestResult<IEnumerable<Application>>.WithError("Falha simulada"));
    }

    private sealed class FailingSavedJobsRepository(IDbContextFactory<ApplicationDbContext> db)
        : SavedJobRepository(db), ISavedJobRepository
    {
        Task<RequestResult<IEnumerable<SavedJob>>> ISavedJobRepository.ListByCandidateAsync(
            Guid candidateId, CancellationToken cancellationToken) =>
            Task.FromResult(RequestResult<IEnumerable<SavedJob>>.WithError("Falha simulada"));
    }

    private sealed class FailingAlertsRepository(IDbContextFactory<ApplicationDbContext> db)
        : JobAlertRepository(db), IJobAlertRepository
    {
        Task<RequestResult<IEnumerable<JobAlert>>> IJobAlertRepository.ListByCandidateAsync(
            Guid candidateId, CancellationToken cancellationToken) =>
            Task.FromResult(RequestResult<IEnumerable<JobAlert>>.WithError("Falha simulada"));
    }
}
