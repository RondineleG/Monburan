using FluentAssertions;

using Microsoft.EntityFrameworkCore;

using Monburan.Core.Entities;
using Monburan.Core.Enums;
using Monburan.Core.ValueObjects;
using Monburan.IntegrationTests.Infrastructure;
using Monburan.Web.Repositories;

using Xunit;

namespace Monburan.IntegrationTests.Repositories;

/// <summary>Vagas salvas no Postgres: índice único (candidato, vaga), idempotência e cascade ao remover a vaga.</summary>
public class SavedJobPostgresTests : IClassFixture<PostgresDatabase>
{
    private readonly PostgresDatabase _database;

    public SavedJobPostgresTests(PostgresDatabase database)
    {
        _database = database;
    }

    private static async Task<(Guid CandidateId, Guid JobId)> SeedCandidateAndJobAsync(Monburan.Web.Data.ApplicationDbContext context)
    {
        var advertiser = new Advertiser
        {
            Id = Id.Create(Guid.NewGuid()),
            Name = "Empresa Teste",
            Email = $"empresa.{Guid.NewGuid():N}@teste.com",
            TenantId = "default"
        };
        var candidate = new Candidate
        {
            Id = Id.Create(Guid.NewGuid()),
            Name = "Candidato Teste",
            Email = $"candidato.{Guid.NewGuid():N}@teste.com",
            TenantId = "default"
        };
        var job = new Job
        {
            Id = Id.Create(Guid.NewGuid()),
            AdvertiserId = advertiser.Id,
            Title = "Desenvolvedor .NET",
            Description = "Descrição da vaga",
            ExpirationDate = DateTime.UtcNow.AddDays(30),
            Status = EJobStatus.Active,
            TenantId = "default"
        };

        context.Advertisers.Add(advertiser);
        context.Candidates.Add(candidate);
        context.Jobs.Add(job);
        await context.SaveChangesAsync();

        return (candidate.Id.Value, job.Id.Value);
    }

    [Fact]
    public async Task SaveAsync_TwiceForTheSamePair_KeepsASingleRow()
    {
        await using var context = _database.CreateContext();
        await context.Database.MigrateAsync();
        var (candidateId, jobId) = await SeedCandidateAndJobAsync(context);

        var repository = new SavedJobRepository(_database.ContextFactory);
        var first = await repository.SaveAsync(NewSavedJob(candidateId, jobId));
        var second = await repository.SaveAsync(NewSavedJob(candidateId, jobId));

        first.Status.Should().Be(Core.Enums.EResultStatus.Success);
        second.Status.Should().Be(Core.Enums.EResultStatus.Success);
        second.Data!.Id.Should().Be(first.Data!.Id, "salvar de novo devolve a linha existente");

        await using var check = _database.CreateContext();
        var count = await check.SavedJobs.CountAsync(item =>
            item.CandidateId == Id.Create(candidateId) && item.JobId == Id.Create(jobId));
        count.Should().Be(1);
    }

    [Fact]
    public async Task UniqueIndex_BlocksTwoRowsForTheSameCandidateAndJob()
    {
        await using var context = _database.CreateContext();
        await context.Database.MigrateAsync();
        var (candidateId, jobId) = await SeedCandidateAndJobAsync(context);

        context.SavedJobs.Add(NewSavedJob(candidateId, jobId));
        await context.SaveChangesAsync();

        await using var other = _database.CreateContext();
        other.SavedJobs.Add(NewSavedJob(candidateId, jobId));

        var act = () => other.SaveChangesAsync();
        await act.Should().ThrowAsync<DbUpdateException>("o índice único (CandidateId, JobId) barra o duplicado");
    }

    [Fact]
    public async Task DeletingTheJob_CascadesToTheSavedRows()
    {
        await using var context = _database.CreateContext();
        await context.Database.MigrateAsync();
        var (candidateId, jobId) = await SeedCandidateAndJobAsync(context);

        context.SavedJobs.Add(NewSavedJob(candidateId, jobId));
        await context.SaveChangesAsync();

        var job = await context.Jobs.SingleAsync(item => item.Id == Id.Create(jobId));
        context.Jobs.Remove(job);
        await context.SaveChangesAsync();

        await using var check = _database.CreateContext();
        var survivors = await check.SavedJobs.CountAsync(item => item.JobId == Id.Create(jobId));
        survivors.Should().Be(0, "remover a vaga leva as vagas salvas junto");
    }

    private static SavedJob NewSavedJob(Guid candidateId, Guid jobId) => new()
    {
        Id = Id.Create(Guid.NewGuid()),
        CandidateId = Id.Create(candidateId),
        JobId = Id.Create(jobId),
        TenantId = "default"
    };
}
