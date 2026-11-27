using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Monburan.Core.Entities;
using Monburan.Core.ValueObjects;
using Monburan.IntegrationTests.Infrastructure;
using Monburan.Web.Services;
using Xunit;

namespace Monburan.IntegrationTests.Services;

public sealed class AccountProfileServicePostgresTests(PostgresDatabase database) : IClassFixture<PostgresDatabase>
{
    [Fact]
    public async Task AnonymizeProfiles_InPostgreSQL_RemovesJsonbResumeSnapshots()
    {
        await using var context = database.CreateContext();
        await context.Database.MigrateAsync();

        var candidateId = Id.Create(Guid.NewGuid());
        var advertiserId = Id.Create(Guid.NewGuid());
        var jobId = Id.Create(Guid.NewGuid());
        var resumeId = Id.Create(Guid.NewGuid());
        context.Candidates.Add(new Candidate { Id = candidateId, Name = "Candidato", Email = "lgpd-pg@teste.com", TenantId = "default" });
        context.Advertisers.Add(new Advertiser { Id = advertiserId, Name = "Empresa", Email = "empresa-pg@teste.com", TenantId = "default" });
        context.Resumes.Add(new Resume { Id = resumeId, CandidateId = candidateId, Title = "Currículo", Summary = "Dados pessoais", TenantId = "default" });
        context.Jobs.Add(new Job { Id = jobId, AdvertiserId = advertiserId, Title = "Vaga", Description = "Descrição", ExpirationDate = DateTime.UtcNow.AddDays(1), TenantId = "default" });
        context.Applications.Add(new Application
        {
            Id = Id.Create(Guid.NewGuid()),
            CandidateId = candidateId,
            JobId = jobId,
            ResumeId = resumeId.Value,
            ResumeSnapshotJson = "{\"summary\":\"Dados pessoais\"}",
            TenantId = "default"
        });
        await context.SaveChangesAsync();

        var service = new AccountProfileService(database.ContextFactory, NullLogger<AccountProfileService>.Instance);
        await service.AnonymizeProfilesAsync("lgpd-pg@teste.com");

        await using var check = database.CreateContext();
        var application = await check.Applications.SingleAsync(a => a.CandidateId == candidateId);
        application.ResumeSnapshotJson.Should().BeNull();
    }
}
