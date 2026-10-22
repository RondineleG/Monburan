using FluentAssertions;

using Microsoft.EntityFrameworkCore;

using Monburan.Core.Entities;
using Monburan.Core.Enums;
using Monburan.Core.ValueObjects;
using Monburan.IntegrationTests.Infrastructure;
using Monburan.Web.Data;
using Monburan.Web.Repositories;

using Xunit;

namespace Monburan.IntegrationTests.Repositories;

/// <summary>
/// Regras que só o PostgreSQL de verdade garante: índice único parcial (que ignora a
/// candidatura removida logicamente) e incremento atômico do contador (que o provider
/// InMemory dos testes de API não exercita).
/// </summary>
public class ApplicationRepositoryPostgresTests : IClassFixture<PostgresDatabase>
{
    private readonly PostgresDatabase _database;

    public ApplicationRepositoryPostgresTests(PostgresDatabase database)
    {
        _database = database;
    }

    private async Task<(Guid CandidateId, Guid JobId)> SeedAsync(ApplicationDbContext context)
    {
        var advertiserId = Id.Create(Guid.NewGuid());
        context.Advertisers.Add(new Advertiser
        {
            Id = advertiserId,
            Name = "Empresa " + Guid.NewGuid().ToString("N")[..6],
            Email = $"empresa-{Guid.NewGuid():N}@teste.com",
            TenantId = "default"
        });

        var candidateId = Id.Create(Guid.NewGuid());
        context.Candidates.Add(new Candidate
        {
            Id = candidateId,
            Name = "Candidato " + Guid.NewGuid().ToString("N")[..6],
            Email = $"candidato-{Guid.NewGuid():N}@teste.com",
            TenantId = "default"
        });

        var jobId = Id.Create(Guid.NewGuid());
        context.Jobs.Add(new Job
        {
            Id = jobId,
            AdvertiserId = advertiserId,
            Title = "Vaga " + Guid.NewGuid().ToString("N")[..6],
            Description = "Descrição",
            ExpirationDate = DateTime.UtcNow.AddDays(30),
            Status = EJobStatus.Active,
            TenantId = "default"
        });

        await context.SaveChangesAsync();
        return (candidateId.Value, jobId.Value);
    }

    [Fact]
    public async Task CreateAsync_AfterASoftDelete_TheCandidateCanApplyAgain()
    {
        await using var context = _database.CreateContext();
        await context.Database.MigrateAsync();
        var (candidateId, jobId) = await SeedAsync(context);

        var repository = new ApplicationRepository(_database.ContextFactory);

        var first = await repository.CreateAsync(new Application
        {
            Id = Id.Create(Guid.NewGuid()),
            CandidateId = Id.Create(candidateId),
            JobId = Id.Create(jobId),
            Status = EApplicationStatus.Sent,
            TenantId = "default"
        });
        first.Status.Should().Be(Core.Enums.EResultStatus.Success);

        // A candidatura removida logicamente não pode mais segurar o e-mail/vaga da pessoa
        var removed = await repository.DeleteAsync(Id.Create(first.Data!.Id.Value));
        removed.Status.Should().Be(Core.Enums.EResultStatus.Success);

        var second = await repository.CreateAsync(new Application
        {
            Id = Id.Create(Guid.NewGuid()),
            CandidateId = Id.Create(candidateId),
            JobId = Id.Create(jobId),
            Status = EApplicationStatus.Sent,
            TenantId = "default"
        });

        second.Status.Should().Be(Core.Enums.EResultStatus.Success,
            "a regra de duplicidade ignora a candidatura removida, então o índice também precisa ignorar");
    }

    [Fact]
    public async Task CreateAsync_TwiceForTheSameJob_IsRejected()
    {
        await using var context = _database.CreateContext();
        await context.Database.MigrateAsync();
        var (candidateId, jobId) = await SeedAsync(context);

        var repository = new ApplicationRepository(_database.ContextFactory);

        var first = await repository.CreateAsync(new Application
        {
            Id = Id.Create(Guid.NewGuid()),
            CandidateId = Id.Create(candidateId),
            JobId = Id.Create(jobId),
            Status = EApplicationStatus.Sent,
            TenantId = "default"
        });
        first.Status.Should().Be(Core.Enums.EResultStatus.Success);

        var second = await repository.CreateAsync(new Application
        {
            Id = Id.Create(Guid.NewGuid()),
            CandidateId = Id.Create(candidateId),
            JobId = Id.Create(jobId),
            Status = EApplicationStatus.Sent,
            TenantId = "default"
        });

        second.Status.Should().NotBe(Core.Enums.EResultStatus.Success,
            "a mesma pessoa não pode se candidatar duas vezes à mesma vaga");
    }

    [Fact]
    public async Task IncrementApplicationsAsync_CountsEveryApplication()
    {
        await using var context = _database.CreateContext();
        await context.Database.MigrateAsync();
        var (candidateId, jobId) = await SeedAsync(context);

        var jobs = new JobRepository(_database.ContextFactory);

        for (var i = 0; i < 3; i++)
        {
            var incremented = await jobs.IncrementApplicationsAsync(jobId);
            incremented.Status.Should().Be(Core.Enums.EResultStatus.Success);
        }

        await using var check = _database.CreateContext();
        var job = await check.Jobs.AsNoTracking().FirstAsync(item => item.Id == Id.Create(jobId));
        job.Counters.Applications.Should().Be(3);
    }

    [Fact]
    public async Task UpdateAsync_DoesNotOverwriteTheModerationOfTheJob()
    {
        await using var context = _database.CreateContext();
        await context.Database.MigrateAsync();
        var (candidateId, jobId) = await SeedAsync(context);

        var jobs = new JobRepository(_database.ContextFactory);

        // A administração modera a vaga
        var moderation = new ModerationInfo
        {
            Status = EModerationStatus.Rejected,
            Reason = "Vaga com informação indevida",
            ModeratorId = "admin-1",
            ModerationDate = DateTime.UtcNow
        };
        var current = (await jobs.GetByIdAsync(Id.Create(jobId))).Data!;
        current.Moderation = moderation;
        current.Status = Core.Enums.EJobStatus.Rejected;
        var moderated = await jobs.UpdateModerationAsync(current);
        moderated.Status.Should().Be(Core.Enums.EResultStatus.Success);

        // A empresa edita a vaga com uma cópia antiga (lida antes da moderação)
        Job staleJob;
        await using (var read = _database.CreateContext())
        {
            staleJob = await read.Jobs.AsNoTracking().FirstAsync(item => item.Id == Id.Create(jobId));
        }

        staleJob.Title = "Vaga editada pela empresa";
        var updated = await jobs.UpdateAsync(staleJob);
        updated.Status.Should().Be(Core.Enums.EResultStatus.Success);

        await using var check = _database.CreateContext();
        var job = await check.Jobs.AsNoTracking().FirstAsync(item => item.Id == Id.Create(jobId));
        job.Title.Should().Be("Vaga editada pela empresa");
        job.Moderation.Should().NotBeNull();
        job.Moderation!.Status.Should().Be(EModerationStatus.Rejected,
            "a edição da empresa não pode apagar o que a administração gravou");
        job.Moderation.Reason.Should().Be("Vaga com informação indevida");
    }
}
