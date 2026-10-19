using FluentAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Monburan.Core.Entities;
using Monburan.Core.Enums;
using Monburan.Core.ValueObjects;
using Monburan.Web.Data;
using Monburan.Web.Repositories;

using Xunit;

namespace Monburan.IntegrationTests.Repositories;

/// <summary>A visão da administração: vagas de qualquer empresa e situação, com busca, filtros e contagens.</summary>
public class JobRepositoryAdminTests : IDisposable
{
    private readonly ServiceProvider _serviceProvider;
    private readonly JobRepository _repository;
    private readonly Advertiser _alfa = new() { Id = Id.Create(Guid.NewGuid()), TenantId = "default", Name = "Alfa Tecnologia", Email = "alfa@exemplo.com", PersonType = EPersonType.Company };
    private readonly Advertiser _beta = new() { Id = Id.Create(Guid.NewGuid()), TenantId = "default", Name = "Beta Marketing", Email = "beta@exemplo.com", PersonType = EPersonType.Company };

    public JobRepositoryAdminTests()
    {
        var databaseName = Guid.NewGuid().ToString();
        var services = new ServiceCollection();
        services.AddDbContextFactory<ApplicationDbContext>(options => options.UseInMemoryDatabase(databaseName));
        _serviceProvider = services.BuildServiceProvider();
        _repository = new JobRepository(_serviceProvider.GetRequiredService<IDbContextFactory<ApplicationDbContext>>());

        using var context = _serviceProvider.GetRequiredService<IDbContextFactory<ApplicationDbContext>>().CreateDbContext();
        context.Advertisers.AddRange(_alfa, _beta);
        context.Jobs.AddRange(
            NewJob(_alfa, "Dev .NET", EJobStatus.Active, "Tecnologia", daysAgo: 1),
            NewJob(_alfa, "Dev Java", EJobStatus.Hidden, "Tecnologia", daysAgo: 2),
            NewJob(_beta, "Analista de SEO", EJobStatus.Paused, "Marketing", daysAgo: 3),
            NewJob(_beta, "Designer", EJobStatus.Rejected, "Design", daysAgo: 4),
            NewJob(_beta, "Vaga excluída", EJobStatus.Active, "Marketing", daysAgo: 5, deleted: true));
        context.SaveChanges();
    }

    public void Dispose() => _serviceProvider.Dispose();

    private static Job NewJob(Advertiser advertiser, string title, EJobStatus status, string category, int daysAgo, bool deleted = false)
    {
        var job = new Job
        {
            Id = Id.Create(Guid.NewGuid()), TenantId = "default", AdvertiserId = advertiser.Id, Title = title, Description = "Descrição.",
            Category = category, Status = status, ExpirationDate = DateTime.UtcNow.AddDays(30), IsDeleted = deleted,
            Location = new JobLocation(), Salary = new SalaryRange(), Moderation = new ModerationInfo(),
            Boost = new JobBoost(), Highlight = new JobHighlight(), Counters = new JobCounters()
        };
        typeof(Monburan.Core.Entities.Base.BaseEntity).GetProperty("CreatedAt")!.SetValue(job, DateTime.UtcNow.AddDays(-daysAgo));
        return job;
    }

    [Fact]
    public async Task ListsEverySituationAndEveryCompany_ExceptDeletedJobs_NewestFirst()
    {
        var result = await _repository.AdminSearchAsync(null, null, null, 1, 20);

        result.TotalCount.Should().Be(4);
        result.Data.Select(job => job.Title).Should().Equal("Dev .NET", "Dev Java", "Analista de SEO", "Designer");
        result.Data.Should().OnlyContain(job => job.Advertiser != null, "a empresa vem junto, para a tela mostrar o nome");
    }

    [Fact]
    public async Task FiltersByStatus()
    {
        (await _repository.AdminSearchAsync(null, EJobStatus.Hidden, null, 1, 20)).Data.Should().ContainSingle().Which.Title.Should().Be("Dev Java");
        (await _repository.AdminSearchAsync(null, EJobStatus.Expired, null, 1, 20)).TotalCount.Should().Be(0);
    }

    [Fact]
    public async Task FiltersByCompany()
    {
        var result = await _repository.AdminSearchAsync(null, null, _beta.Id.Value, 1, 20);

        result.Data.Select(job => job.Title).Should().BeEquivalentTo("Analista de SEO", "Designer");
    }

    [Theory]
    [InlineData("dev", 2)]
    [InlineData("TECNOLOGIA", 2)]
    [InlineData("beta", 2)]
    [InlineData("alfa tec", 2)]
    [InlineData("seo", 1)]
    [InlineData("não existe", 0)]
    public async Task SearchLooksAtTheTitleTheCategoryAndTheCompanyName_IgnoringCase(string term, int expected)
    {
        (await _repository.AdminSearchAsync(term, null, null, 1, 20)).TotalCount.Should().Be(expected);
    }

    [Fact]
    public async Task Pages()
    {
        var first = await _repository.AdminSearchAsync(null, null, null, 1, 3);
        var second = await _repository.AdminSearchAsync(null, null, null, 2, 3);

        first.Data.Should().HaveCount(3);
        first.TotalPages.Should().Be(2);
        second.Data.Should().ContainSingle().Which.Title.Should().Be("Designer");
    }

    [Fact]
    public async Task CountsByStatus_IgnoringDeletedJobs()
    {
        var counts = await _repository.CountByStatusAsync();

        counts[EJobStatus.Active].Should().Be(1);
        counts[EJobStatus.Hidden].Should().Be(1);
        counts[EJobStatus.Paused].Should().Be(1);
        counts[EJobStatus.Rejected].Should().Be(1);
        counts.ContainsKey(EJobStatus.Expired).Should().BeFalse();
    }
}
