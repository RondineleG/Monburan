using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Monburan.Core.Entities;
using Monburan.Core.Enums;
using Monburan.Core.Services;
using Monburan.Core.UseCases.ResumeShares;
using Monburan.Core.ValueObjects;
using Monburan.Web.Data;
using Monburan.Web.Repositories;
using Monburan.Web.Services;

namespace Monburan.ApiTests;

public sealed class ResumeShareDeletionTests(MonburanApiFactory factory) : IClassFixture<MonburanApiFactory>
{
    private IDbContextFactory<ApplicationDbContext> Db => factory.Services.GetRequiredService<IDbContextFactory<ApplicationDbContext>>();

    private async Task<(Candidate Candidate, Resume Resume, string Token)> SeedAsync()
    {
        var candidate = new Candidate
        {
            Id = Id.Create(Guid.NewGuid()), Name = "Nome privado", Email = $"{Guid.NewGuid():N}@teste.com", TenantId = "default"
        };
        var resume = new Resume
        {
            Id = Id.Create(Guid.NewGuid()), CandidateId = candidate.Id, Title = "Currículo privado", Summary = "Resumo privado", TenantId = "default",
            ExperienceHistory = [new Experience { Position = "Experiência privada", Company = "Empresa privada", StartDate = DateTime.UtcNow.AddYears(-1) }]
        };
        var token = ResumeShareRules.NewToken();
        await using var db = await Db.CreateDbContextAsync();
        db.Candidates.Add(candidate);
        db.Resumes.Add(resume);
        db.ResumeShares.Add(new ResumeShare
        {
            Id = Id.Create(Guid.NewGuid()), ResumeId = resume.Id, TokenHash = ResumeShareRules.Hash(token),
            ShowName = true, ShowSummary = true, ShowExperience = true, TenantId = "default"
        });
        await db.SaveChangesAsync();
        return (candidate, resume, token);
    }

    [Fact]
    public async Task DeletingAResume_RevokesItsLinks_AndKeepsOtherResumesAvailable()
    {
        var (_, resume, token) = await SeedAsync();
        var (_, _, otherToken) = await SeedAsync();
        var get = new GetPublicResumeUseCase(new ResumeShareRepository(Db), TimeProvider.System);
        (await get.ExecuteAsync(token)).Status.Should().Be(EResultStatus.Success);

        (await new ResumeRepository(Db).DeleteAsync(resume.Id)).Status.Should().Be(EResultStatus.Success);

        (await get.ExecuteAsync(token)).Status.Should().Be(EResultStatus.EntityNotFound);
        (await get.ExecuteAsync(otherToken)).Status.Should().Be(EResultStatus.Success);
        await using var db = await Db.CreateDbContextAsync();
        (await db.ResumeShares.SingleAsync(s => s.ResumeId == resume.Id)).RevokedAt.Should().NotBeNull();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task PublicLookup_RefusesLegacyLinksWhoseResumeOrCandidateWasDeleted(bool deleteResume)
    {
        var (candidate, resume, token) = await SeedAsync();
        await using (var db = await Db.CreateDbContextAsync())
        {
            if (deleteResume)
                (await db.Resumes.FindAsync(resume.Id))!.IsDeleted = true;
            else
                (await db.Candidates.FindAsync(candidate.Id))!.IsDeleted = true;
            await db.SaveChangesAsync();
        }

        var repository = new ResumeShareRepository(Db);
        (await repository.GetByTokenHashAsync(ResumeShareRules.Hash(token))).Status.Should().Be(EResultStatus.EntityNotFound);
        (await new GetPublicResumeUseCase(repository, TimeProvider.System).ExecuteAsync(token)).Status.Should().Be(EResultStatus.EntityNotFound);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AnonymizingTheAccount_RevokesLinksIncludingAlreadyDeletedResumes(bool previouslyDeleted)
    {
        var (candidate, resume, token) = await SeedAsync();
        if (previouslyDeleted)
        {
            await using var existing = await Db.CreateDbContextAsync();
            (await existing.Resumes.FindAsync(resume.Id))!.IsDeleted = true;
            await existing.SaveChangesAsync();
        }

        using var scope = factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<AccountProfileService>().AnonymizeProfilesAsync(candidate.Email);

        var get = new GetPublicResumeUseCase(new ResumeShareRepository(Db), TimeProvider.System);
        (await get.ExecuteAsync(token)).Status.Should().Be(EResultStatus.EntityNotFound);
        await using var db = await Db.CreateDbContextAsync();
        (await db.ResumeShares.SingleAsync(s => s.ResumeId == resume.Id)).RevokedAt.Should().NotBeNull();
        (await db.Resumes.FindAsync(resume.Id))!.Summary.Should().BeNull();
    }
}
