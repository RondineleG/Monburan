using FluentAssertions;

using Microsoft.EntityFrameworkCore;

using Monburan.Core.Entities;
using Monburan.Core.Enums;
using Monburan.Core.ValueObjects;
using Monburan.IntegrationTests.Infrastructure;
using Monburan.Web.Repositories;

using Xunit;

namespace Monburan.IntegrationTests.Repositories;

/// <summary>
/// Busca com os filtros novos e o contador de visualizações no Postgres de verdade:
/// é o provider relacional que traduz o ExecuteUpdateAsync dos contadores.
/// </summary>
public class JobRepositoryPostgresTests : IClassFixture<PostgresDatabase>
{
    private readonly PostgresDatabase _database;

    public JobRepositoryPostgresTests(PostgresDatabase database)
    {
        _database = database;
    }

    private async Task<Guid> SeedJobAsync(
        Monburan.Web.Data.ApplicationDbContext context,
        Guid advertiserId,
        EContractType? contractType = null,
        ESalaryPeriod? salaryPeriod = null,
        decimal? salaryMin = null,
        decimal? salaryMax = null)
    {
        var job = new Job
        {
            Id = Id.Create(Guid.NewGuid()),
            AdvertiserId = Id.Create(advertiserId),
            Title = "Desenvolvedor .NET",
            Description = "Descrição da vaga",
            ExpirationDate = DateTime.UtcNow.AddDays(30),
            Status = EJobStatus.Active,
            ContractType = contractType,
            Salary = salaryMin is null && salaryMax is null && salaryPeriod is null
                ? null
                : new SalaryRange { Min = salaryMin, Max = salaryMax, Period = salaryPeriod, Currency = "BRL", Display = true },
            TenantId = "default"
        };
        context.Jobs.Add(job);
        await context.SaveChangesAsync();
        return job.Id.Value;
    }

    private async Task<Guid> SeedAdvertiserAsync(Monburan.Web.Data.ApplicationDbContext context)
    {
        var advertiser = new Advertiser
        {
            Id = Id.Create(Guid.NewGuid()),
            Name = "Empresa Teste",
            Email = $"empresa.{Guid.NewGuid():N}@teste.com",
            TenantId = "default"
        };
        context.Advertisers.Add(advertiser);
        await context.SaveChangesAsync();
        return advertiser.Id.Value;
    }

    [Fact]
    public async Task IncrementViewsAsync_OnRealDatabase_IncrementsAtomically()
    {
        await using var context = _database.CreateContext();
        await context.Database.MigrateAsync();
        var advertiserId = await SeedAdvertiserAsync(context);
        var jobId = await SeedJobAsync(context, advertiserId);

        var repository = new JobRepository(_database.ContextFactory);

        (await repository.IncrementViewsAsync(jobId)).Data.Should().BeTrue();
        (await repository.IncrementViewsAsync(jobId)).Data.Should().BeTrue();

        await using var check = _database.CreateContext();
        var job = await check.Jobs.AsNoTracking().SingleAsync(item => item.Id == Id.Create(jobId));
        job.Counters.Views.Should().Be(2);

        (await repository.IncrementViewsAsync(Guid.NewGuid())).Data.Should().BeFalse("vaga inexistente não tem contador");
    }

    [Fact]
    public async Task SearchAsync_WithContractTypeAndSalaryPeriod_FiltersOnTheDatabase()
    {
        await using var context = _database.CreateContext();
        await context.Database.MigrateAsync();
        var advertiserId = await SeedAdvertiserAsync(context);
        var cltMonth = await SeedJobAsync(context, advertiserId, EContractType.Clt, ESalaryPeriod.Month, 3000, 5000);
        await SeedJobAsync(context, advertiserId, EContractType.Pj, ESalaryPeriod.Year, 60000, 80000);

        var repository = new JobRepository(_database.ContextFactory);

        var clt = await repository.SearchAsync(contractType: EContractType.Clt);
        clt.Data!.Data.Select(job => job.Id.Value).Should().BeEquivalentTo([cltMonth]);

        var monthly = await repository.SearchAsync(salaryPeriod: ESalaryPeriod.Month);
        monthly.Data!.Data.Select(job => job.Id.Value).Should().BeEquivalentTo([cltMonth]);

        var both = await repository.SearchAsync(contractType: EContractType.Pj, salaryPeriod: ESalaryPeriod.Month);
        both.Data!.Data.Should().BeEmpty("os filtros precisam funcionar juntos");
    }

    [Fact]
    public async Task SearchAsync_PublishedWithinDays_LeavesOutTheOldJobs()
    {
        await using var context = _database.CreateContext();
        await context.Database.MigrateAsync();
        var advertiserId = await SeedAdvertiserAsync(context);
        var recent = await SeedJobAsync(context, advertiserId);
        var old = await SeedJobAsync(context, advertiserId);

        await context.Database.ExecuteSqlAsync(
            $"""UPDATE "Jobs" SET "CreatedAt" = {DateTime.UtcNow.AddDays(-40)} WHERE "Id" = {old}""");

        var repository = new JobRepository(_database.ContextFactory);
        var result = await repository.SearchAsync(publishedWithinDays: 7);

        result.Data!.Data.Select(job => job.Id.Value).Should().BeEquivalentTo([recent]);
    }
}
