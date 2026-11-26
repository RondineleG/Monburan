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
        decimal? salaryMax = null,
        string? title = null)
    {
        var job = new Job
        {
            Id = Id.Create(Guid.NewGuid()),
            AdvertiserId = Id.Create(advertiserId),
            Title = title ?? "Desenvolvedor .NET",
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
        var marker = $"Filtro {Guid.NewGuid():N}";
        var recent = await SeedJobAsync(context, advertiserId, title: marker);
        var old = await SeedJobAsync(context, advertiserId, title: marker);
        var oldDraftPublishedNow = await SeedJobAsync(context, advertiserId, title: marker);

        await context.Database.ExecuteSqlAsync(
            $"""UPDATE "Jobs" SET "CreatedAt" = {DateTime.UtcNow.AddDays(-40)} WHERE "Id" = {old}""");
        await context.Database.ExecuteSqlAsync(
            $"""UPDATE "Jobs" SET "CreatedAt" = {DateTime.UtcNow.AddDays(-40)}, "PublishedAt" = {DateTime.UtcNow} WHERE "Id" = {oldDraftPublishedNow}""");

        var repository = new JobRepository(_database.ContextFactory);
        var result = await repository.SearchAsync(searchTerm: marker, publishedWithinDays: 7);

        result.Data!.Data.Select(job => job.Id.Value).Should().BeEquivalentTo([recent, oldDraftPublishedNow]);
    }
    [Fact]
    public async Task SearchAsync_DistinguishesCreationSortFromPublicationRecency()
    {
        await using var context = _database.CreateContext();
        await context.Database.MigrateAsync();
        var advertiserId = await SeedAdvertiserAsync(context);
        var marker = $"Ordenação {Guid.NewGuid():N}";
        var createdRecentlyPublishedBefore = await SeedJobAsync(context, advertiserId, title: marker);
        var createdBeforePublishedRecently = await SeedJobAsync(context, advertiserId, title: marker);

        await context.Database.ExecuteSqlAsync(
            $"""UPDATE "Jobs" SET "CreatedAt" = {DateTime.UtcNow.AddMinutes(-10)}, "PublishedAt" = {DateTime.UtcNow.AddDays(-5)} WHERE "Id" = {createdRecentlyPublishedBefore}""");
        await context.Database.ExecuteSqlAsync(
            $"""UPDATE "Jobs" SET "CreatedAt" = {DateTime.UtcNow.AddDays(-10)}, "PublishedAt" = {DateTime.UtcNow.AddMinutes(-1)} WHERE "Id" = {createdBeforePublishedRecently}""");

        var repository = new JobRepository(_database.ContextFactory);

        var byPublication = await repository.SearchAsync(searchTerm: marker, sortBy: "publishedAt");
        byPublication.Data!.Data.Select(job => job.Id.Value).Should().Equal(createdBeforePublishedRecently, createdRecentlyPublishedBefore);

        var byCreation = await repository.SearchAsync(searchTerm: marker, sortBy: "createdAt");
        byCreation.Data!.Data.Select(job => job.Id.Value).Should().Equal(createdRecentlyPublishedBefore, createdBeforePublishedRecently);
    }

    [Fact]
    public async Task ListRecentForRecommendationsAsync_ExcludesJobsBeforeLimiting()
    {
        await using var context = _database.CreateContext();
        await context.Database.MigrateAsync();
        var advertiserId = await SeedAdvertiserAsync(context);
        var older = await SeedJobAsync(context, advertiserId);
        var newer = await SeedJobAsync(context, advertiserId);
        var repository = new JobRepository(_database.ContextFactory);

        var result = await repository.ListRecentForRecommendationsAsync(1, [Id.Create(newer)]);

        result.Status.Should().Be(Core.Enums.EResultStatus.Success);
        result.Data!.Select(job => job.Id.Value).Should().BeEquivalentTo([older]);

        await context.Database.ExecuteSqlAsync(
            $"""UPDATE "Jobs" SET "IsDeleted" = true WHERE "Id" IN ({older}, {newer})""");
    }

    [Fact]
    public async Task UpdateAsync_WhenStoredJobIsClosed_DoesNotReopenIt()
    {
        await using var context = _database.CreateContext();
        await context.Database.MigrateAsync();
        var advertiserId = await SeedAdvertiserAsync(context);
        var jobId = await SeedJobAsync(context, advertiserId);
        var closedBy = Guid.NewGuid().ToString();
        await context.Database.ExecuteSqlAsync(
            $"""UPDATE "Jobs" SET "Status" = {"Closed"}, "UpdatedBy" = {closedBy} WHERE "Id" = {jobId}""");

        var staleEdit = new Job
        {
            Id = Id.Create(jobId),
            AdvertiserId = Id.Create(advertiserId),
            Title = "Edição concorrente",
            Description = "Edição iniciada antes do encerramento",
            ExpirationDate = DateTime.UtcNow.AddDays(30),
            Status = EJobStatus.Active,
            UpdatedBy = advertiserId.ToString(),
            TenantId = "default"
        };
        var repository = new JobRepository(_database.ContextFactory);

        var result = await repository.UpdateAsync(staleEdit);

        result.Status.Should().Be(Core.Enums.EResultStatus.Success);
        await using var check = _database.CreateContext();
        var stored = await check.Jobs.AsNoTracking().SingleAsync(job => job.Id == Id.Create(jobId));
        stored.Status.Should().Be(EJobStatus.Closed);
        stored.Title.Should().Be("Edição concorrente");
        stored.UpdatedBy.Should().Be(closedBy);
    }


    [Fact]
    public async Task UpdateAsync_WhenModerationHidesJob_DoesNotReopenIt()
    {
        await using var context = _database.CreateContext();
        await context.Database.MigrateAsync();
        var advertiserId = await SeedAdvertiserAsync(context);
        var jobId = await SeedJobAsync(context, advertiserId);
        await context.Database.ExecuteSqlAsync(
            $"""UPDATE "Jobs" SET "Status" = {"Hidden"} WHERE "Id" = {jobId}""");

        var staleEdit = new Job
        {
            Id = Id.Create(jobId),
            AdvertiserId = Id.Create(advertiserId),
            Title = "Edição durante moderação",
            Description = "Edição iniciada antes da ocultação",
            ExpirationDate = DateTime.UtcNow.AddDays(30),
            Status = EJobStatus.Active,
            UpdatedBy = advertiserId.ToString(),
            TenantId = "default"
        };
        var repository = new JobRepository(_database.ContextFactory);

        var result = await repository.UpdateAsync(staleEdit);

        result.Status.Should().Be(Core.Enums.EResultStatus.Success);
        await using var check = _database.CreateContext();
        var stored = await check.Jobs.AsNoTracking().SingleAsync(job => job.Id == Id.Create(jobId));
        stored.Status.Should().Be(EJobStatus.Hidden);
        stored.Title.Should().Be("Edição durante moderação");
    }


    [Fact]
    public async Task UpdateStatusAsync_WhenClosingJob_PersistsStatusAndAuthor()
    {
        await using var context = _database.CreateContext();
        await context.Database.MigrateAsync();
        var advertiserId = await SeedAdvertiserAsync(context);
        var jobId = await SeedJobAsync(context, advertiserId);
        var close = new Job
        {
            Id = Id.Create(jobId),
            AdvertiserId = Id.Create(advertiserId),
            Status = EJobStatus.Closed,
            TenantId = "default"
        };
        var repository = new JobRepository(_database.ContextFactory);

        var result = await repository.UpdateStatusAsync(close, EJobStatus.Active, advertiserId.ToString());

        result.Status.Should().Be(Core.Enums.EResultStatus.Success);
        await using var check = _database.CreateContext();
        var stored = await check.Jobs.AsNoTracking().SingleAsync(job => job.Id == Id.Create(jobId));
        stored.Status.Should().Be(EJobStatus.Closed);
        stored.UpdatedBy.Should().Be(advertiserId.ToString());
    }

    [Fact]
    public async Task EditAndPublication_DoNotOverwriteEachOther()
    {
        await using var context = _database.CreateContext();
        await context.Database.MigrateAsync();
        var advertiserId = await SeedAdvertiserAsync(context);
        var jobId = await SeedJobAsync(context, advertiserId);
        await context.Database.ExecuteSqlAsync(
            $"""UPDATE "Jobs" SET "Status" = {"Draft"}, "PublishedAt" = NULL WHERE "Id" = {jobId}""");
        var repository = new JobRepository(_database.ContextFactory);

        var edit = new Job
        {
            Id = Id.Create(jobId),
            AdvertiserId = Id.Create(advertiserId),
            Title = "Rascunho editado",
            Description = "Conteúdo novo do rascunho",
            ExpirationDate = DateTime.UtcNow.AddDays(30),
            Status = EJobStatus.Draft,
            UpdatedBy = advertiserId.ToString(),
            TenantId = "default"
        };
        var publish = new Job { Id = Id.Create(jobId), Status = EJobStatus.Active, TenantId = "default" };
        var editTask = repository.UpdateAsync(edit);
        var publishTask = repository.UpdateStatusAsync(publish, EJobStatus.Draft, advertiserId.ToString());
        var results = await Task.WhenAll(editTask, publishTask);
        results.Should().OnlyContain(result => result.Status == Core.Enums.EResultStatus.Success);

        await using var check = _database.CreateContext();
        var stored = await check.Jobs.AsNoTracking().SingleAsync(job => job.Id == Id.Create(jobId));
        stored.Status.Should().Be(EJobStatus.Active);
        stored.Title.Should().Be("Rascunho editado");
        stored.PublishedAt.Should().NotBeNull();

        var staleEdit = new Job
        {
            Id = Id.Create(jobId),
            AdvertiserId = Id.Create(advertiserId),
            Title = "Edição iniciada antes da publicação",
            Description = "Conteúdo ainda mais novo",
            ExpirationDate = DateTime.UtcNow.AddDays(30),
            Status = EJobStatus.Draft,
            UpdatedBy = advertiserId.ToString(),
            TenantId = "default"
        };
        (await repository.UpdateAsync(staleEdit)).Status.Should().Be(Core.Enums.EResultStatus.Success);

        await using var finalCheck = _database.CreateContext();
        var finalJob = await finalCheck.Jobs.AsNoTracking().SingleAsync(job => job.Id == Id.Create(jobId));
        finalJob.Status.Should().Be(EJobStatus.Active, "edição de conteúdo não pode voltar a vaga para rascunho");
        finalJob.Title.Should().Be("Edição iniciada antes da publicação");
    }

}

