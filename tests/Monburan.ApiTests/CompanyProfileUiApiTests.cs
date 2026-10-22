using System.Net;
using System.Text.RegularExpressions;

using FluentAssertions;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

using Monburan.Core.DTOs.Response;
using Monburan.Core.Entities;
using Monburan.Core.Enums;
using Monburan.Core.Interfaces.Repositories;
using Monburan.Core.Requests;
using Monburan.Web.Data;
using Monburan.Web.Repositories;

namespace Monburan.ApiTests;

public class CompanyProfileUiApiTests
{
    [Fact]
    public async Task FailedJobSearch_KeepsCompanyButShowsRetryInsteadOfEmptyState()
    {
        await using var factory = new FailingCompanyJobsFactory();
        using var client = factory.CreateClient();
        using var db = factory.Services.GetRequiredService<IDbContextFactory<ApplicationDbContext>>().CreateDbContext();
        var job = db.Jobs.First();

        var response = await client.GetAsync($"/companies/{job.AdvertiserId.Value}");
        var html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        html.Should().Contain("Vagas da empresa")
            .And.Contain("Não foi possível carregar as vagas")
            .And.Contain("data-testid=\"error-retry\"");
        html.Should().NotContain("Nenhuma vaga aberta");
    }

    [Fact]
    public async Task MoreThanOnePageOfPublicJobs_HasLinkPagination()
    {
        await using var factory = new MonburanApiFactory();
        using var client = factory.CreateClient();
        await client.GetAsync("/health");
        await using var db = await factory.Services
            .GetRequiredService<IDbContextFactory<ApplicationDbContext>>()
            .CreateDbContextAsync();

        var existingJob = db.Jobs.First();
        var advertiserId = existingJob.AdvertiserId.Value;
        var tenantId = existingJob.TenantId;
        var initialPublicCount = db.Jobs.Count(job =>
            job.AdvertiserId == existingJob.AdvertiserId && job.Status == EJobStatus.Active && !job.IsDeleted);
        db.Jobs.AddRange(Enumerable.Range(1, 21).Select(index => new Job
        {
            AdvertiserId = existingJob.AdvertiserId,
            TenantId = tenantId,
            Title = $"Vaga adicional {index:00}",
            Description = "Vaga criada no banco isolado de testes.",
            ExpirationDate = DateTime.UtcNow.AddDays(20),
            NumberOfOpenings = 1,
            Status = EJobStatus.Active
        }));
        await db.SaveChangesAsync();

        var response = await client.GetAsync($"/companies/{advertiserId}?pagina=2");
        var html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
        var total = initialPublicCount + 21;
        var range = Regex.Match(html, "data-testid=\"pager-range\">(?<range>.*?)</p>", RegexOptions.Singleline)
            .Groups["range"].Value;

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        Regex.Replace(range, @"\s+", " ").Should().Be($"Mostrando 21\u2013{total} de {total} vagas");
        html.Should().Contain($"href=\"/companies/{advertiserId}\"");
        Regex.Matches(html, "data-testid=\"job-card\"").Should().HaveCount(total - 20);
    }

    [Fact]
    public async Task FailedJobFilter_DoesNotHideReceivedApplications()
    {
        await using var factory = new FailingCompanyJobsFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true
        });
        await CookieAuth.LoginAsync(client, "contato@techsolutions.com", "Monburan123");

        var response = await client.GetAsync("/received-applications");
        var html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        html.Should().Contain("data-testid=\"jobs-filter-incomplete\"")
            .And.Contain("data-testid=\"applications-results\"");
        html.Should().NotContain("data-testid=\"error-state\"");
    }

    [Theory]
    [InlineData("/resumes", 1)]
    [InlineData("/resumes/new", 2)]
    public async Task AuthenticatedTrail_MarksExactlyTheFinalStepAsCurrent(string path, int steps)
    {
        await using var factory = new MonburanApiFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true
        });
        await CookieAuth.LoginAsync(client, "joao.silva@email.com", "Monburan123");

        var response = await client.GetAsync(path);
        var html = await response.Content.ReadAsStringAsync();
        var trail = Regex.Match(html, "<ol class=\"trail\"[^>]*>(.*?)</ol>", RegexOptions.Singleline);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        trail.Success.Should().BeTrue();
        Regex.Matches(trail.Groups[1].Value, "class=\"trail__item\"").Should().HaveCount(steps);
        Regex.Matches(trail.Groups[1].Value, "aria-current=\"page\"").Should().ContainSingle();
        trail.Groups[1].Value.Should().MatchRegex("aria-current=\"page\"[^>]*>\\s*<span\\b");
    }

    private sealed class FailingCompanyJobsFactory : MonburanApiFactory
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IJobRepository>();
                services.AddScoped<IJobRepository>(provider => new FailingCompanyJobsRepository(
                    provider.GetRequiredService<IDbContextFactory<ApplicationDbContext>>()));
            });
        }
    }

    private sealed class FailingCompanyJobsRepository(IDbContextFactory<ApplicationDbContext> db)
        : JobRepository(db), IJobRepository
    {
        Task<RequestResult<PagedResponse<Job>>> IJobRepository.SearchAsync(
            string? searchTerm, string? city, string? state, bool? isRemote,
            decimal? salaryMin, decimal? salaryMax, string? category, Guid? advertiserId,
            int page, int size, string? type, string? sortBy, bool? sortDescending,
            EContractType? contractType, ESalaryPeriod? salaryPeriod, int? publishedWithinDays,
            CancellationToken cancellationToken) =>
            Task.FromResult(RequestResult<PagedResponse<Job>>.WithError("Falha simulada na busca de vagas"));

        Task<RequestResult<PagedResponse<Job>>> IJobRepository.SearchPublicByAdvertiserAsync(
            Guid advertiserId, int page, int size, CancellationToken cancellationToken) =>
            Task.FromResult(RequestResult<PagedResponse<Job>>.WithError("Falha simulada na busca de vagas"));
    }
}
