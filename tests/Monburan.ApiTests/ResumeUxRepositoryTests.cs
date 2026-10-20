using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Monburan.Core.Entities;
using Monburan.Core.Enums;
using Monburan.Core.ValueObjects;
using Monburan.Web.Data;
using Monburan.Web.Repositories;

namespace Monburan.ApiTests;

/// <summary>
/// Consultas exatas da revisão de UX contra o ApplicationRepository real (EF InMemory
/// da factory, sem Testcontainers e sem banco compartilhado).
/// </summary>
public sealed class ResumeUxRepositoryTests : IClassFixture<MonburanApiFactory>
{
    private readonly MonburanApiFactory _factory;

    public ResumeUxRepositoryTests(MonburanApiFactory factory) => _factory = factory;

    private ApplicationRepository NewRepository() =>
        new(_factory.Services.GetRequiredService<IDbContextFactory<ApplicationDbContext>>());

    private async Task<ApplicationDbContext> NewDbAsync() =>
        await _factory.Services.GetRequiredService<IDbContextFactory<ApplicationDbContext>>().CreateDbContextAsync();

    private static Job NewJob(Guid advertiserId, string title = "Vaga") => new()
    {
        AdvertiserId = Id.Create(advertiserId),
        Title = title,
        Description = "Descrição",
        Status = EJobStatus.Active,
        Location = new JobLocation(),
        Moderation = new ModerationInfo { Status = EModerationStatus.Approved },
        Salary = new SalaryRange(),
        Counters = new JobCounters()
    };

    private static Application NewApplication(Guid candidateId, Guid jobId, Guid? resumeId = null, bool deleted = false) =>
        new()
        {
            CandidateId = Id.Create(candidateId),
            JobId = Id.Create(jobId),
            ResumeId = resumeId,
            Status = EApplicationStatus.Sent,
            AppliedAt = DateTime.UtcNow,
            IsDeleted = deleted
        };

    private async Task<Job> AddJobAsync(Guid advertiserId)
    {
        await using var db = await NewDbAsync();
        var job = NewJob(advertiserId);
        db.Jobs.Add(job);
        await db.SaveChangesAsync();
        return job;
    }

    [Fact]
    public async Task FindByCandidateAndJob_ExactMatch_Found()
    {
        var candidateId = Guid.NewGuid();
        var job = await AddJobAsync(Guid.NewGuid());
        await using (var db = await NewDbAsync())
        {
            db.Applications.Add(NewApplication(candidateId, job.Id.Value));
            db.Applications.Add(NewApplication(Guid.NewGuid(), job.Id.Value)); // outro candidato, mesma vaga
            await db.SaveChangesAsync();
        }

        var found = await NewRepository().FindByCandidateAndJobAsync(candidateId, job.Id.Value);

        Assert.Equal(EResultStatus.Success, found.Status);
        Assert.NotNull(found.Data);
        Assert.Equal(candidateId, found.Data!.CandidateId.Value);
    }

    [Fact]
    public async Task FindByCandidateAndJob_NoMatchOrSoftDeleted_Null()
    {
        var candidateId = Guid.NewGuid();
        var job = await AddJobAsync(Guid.NewGuid());
        var otherJob = await AddJobAsync(Guid.NewGuid());
        await using (var db = await NewDbAsync())
        {
            // candidatura em outra vaga + candidatura excluída nesta vaga
            db.Applications.Add(NewApplication(candidateId, otherJob.Id.Value));
            db.Applications.Add(NewApplication(candidateId, job.Id.Value, deleted: true));
            await db.SaveChangesAsync();
        }

        var found = await NewRepository().FindByCandidateAndJobAsync(candidateId, job.Id.Value);

        Assert.Equal(EResultStatus.Success, found.Status);
        Assert.Null(found.Data);
    }

    [Fact]
    public async Task CanAdvertiserViewResume_OnlyExactAttachment()
    {
        var candidateId = Guid.NewGuid();
        var employer = Guid.NewGuid();
        var otherEmployer = Guid.NewGuid();
        var resumeId = Guid.NewGuid();
        var otherResumeId = Guid.NewGuid();

        var employerJob = await AddJobAsync(employer);
        var otherEmployerJob = await AddJobAsync(otherEmployer);

        await using (var db = await NewDbAsync())
        {
            // anexo deste currículo nesta vaga do empregador
            db.Applications.Add(NewApplication(candidateId, employerJob.Id.Value, resumeId));
            // mesmo candidato anexou OUTRO currículo, mas na vaga de outro empregador
            db.Applications.Add(NewApplication(candidateId, otherEmployerJob.Id.Value, otherResumeId));
            // outro candidato anexou o mesmo resumeId em outra vaga (id solto não basta)
            db.Applications.Add(NewApplication(Guid.NewGuid(), otherEmployerJob.Id.Value, resumeId));
            await db.SaveChangesAsync();
        }

        var repo = NewRepository();

        Assert.True(await repo.CanAdvertiserViewResumeAsync(employer, resumeId, candidateId));
        // empregador certo mas currículo que não foi anexado a ele
        Assert.False(await repo.CanAdvertiserViewResumeAsync(employer, otherResumeId, candidateId));
        Assert.False(await repo.CanAdvertiserViewResumeAsync(employer, Guid.NewGuid(), candidateId));
        // outro empregador não vê o anexo
        Assert.False(await repo.CanAdvertiserViewResumeAsync(otherEmployer, resumeId, candidateId));
        // empregador certo, candidato errado
        Assert.False(await repo.CanAdvertiserViewResumeAsync(employer, resumeId, Guid.NewGuid()));
    }

    [Fact]
    public async Task CanAdvertiserViewResume_Beyond200Applications_StillFound()
    {
        // Regressão: antes o recrutador era autorizado por lista limitada a 200 — um
        // anexo antigo além do limite não aparecia. A consulta exata não tem teto.
        var candidateId = Guid.NewGuid();
        var employer = Guid.NewGuid();
        var resumeId = Guid.NewGuid();
        var employerJob = await AddJobAsync(employer);

        await using (var db = await NewDbAsync())
        {
            db.Applications.Add(NewApplication(candidateId, employerJob.Id.Value, resumeId));
            for (var i = 0; i < 210; i++)
                db.Applications.Add(NewApplication(Guid.NewGuid(), employerJob.Id.Value));
            await db.SaveChangesAsync();
        }

        Assert.True(await NewRepository().CanAdvertiserViewResumeAsync(employer, resumeId, candidateId));
    }
}
