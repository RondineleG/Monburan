using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Monburan.Core.Entities;
using Monburan.Core.Enums;
using Monburan.Core.ValueObjects;
using Monburan.IntegrationTests.Infrastructure;
using Monburan.Web.Repositories;

namespace Monburan.IntegrationTests.Repositories;

public sealed class JobSubmissionRepositoryPostgresTests(PostgresDatabase database) : IClassFixture<PostgresDatabase>
{
    [Fact]
    public async Task ConcurrentDecisions_OnlyOneIsSaved_WithItsOwnHistory()
    {
        await using (var db = database.CreateContext())
            await db.Database.MigrateAsync();
        var repository = new JobSubmissionRepository(database.ContextFactory);
        var submission = new JobSubmission
        {
            Id = Id.Create(Guid.NewGuid()), Title = "Sugestão concorrente", Status = ESubmissionStatus.Pending, TenantId = "default"
        };
        (await repository.CreateAsync(submission)).Status.Should().Be(EResultStatus.Success);
        var decisions = new List<JobSubmission>();
        foreach (var status in new[] { ESubmissionStatus.Approved, ESubmissionStatus.Rejected })
        {
            var decision = (await repository.GetByIdAsync(submission.Id)).Data!;
            decision.Status = status;
            decision.ModeratedBy = status.ToString();
            decision.History.Add(new JobSubmissionHistory
            {
                Id = Id.Create(Guid.NewGuid()), SubmissionId = submission.Id, Status = status, ActorId = status.ToString(), TenantId = "default"
            });
            decisions.Add(decision);
        }

        var results = await Task.WhenAll(decisions.Select(d => repository.UpdateAsync(d)));

        results.Count(r => r.Status == EResultStatus.Success).Should().Be(1);
        results.Count(r => r.Status == EResultStatus.DomainError).Should().Be(1);
        var saved = (await repository.GetByIdAsync(submission.Id)).Data!;
        saved.History.Should().ContainSingle();
        saved.History.Single().Status.Should().Be(saved.Status);
        saved.ModeratedBy.Should().Be(saved.Status.ToString());
    }
}
