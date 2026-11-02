using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Monburan.Core.Entities;
using Monburan.Core.Enums;
using Monburan.Core.ValueObjects;
using Monburan.Web.Data;
using Monburan.Web.Repositories;

namespace Monburan.ApiTests;

public sealed class JobSubmissionPersistenceTests(MonburanApiFactory factory) : IClassFixture<MonburanApiFactory>
{
    private JobSubmissionRepository Repository => new(factory.Services.GetRequiredService<IDbContextFactory<ApplicationDbContext>>());

    private async Task<JobSubmission> SeedAsync()
    {
        var submission = new JobSubmission { Id = Id.Create(Guid.NewGuid()), Title = "Vaga de teste", Status = ESubmissionStatus.Pending, TenantId = "default" };
        (await Repository.CreateAsync(submission)).Status.Should().Be(EResultStatus.Success);
        return submission;
    }

    [Theory]
    [InlineData(ESubmissionStatus.Approved)]
    [InlineData(ESubmissionStatus.Rejected)]
    [InlineData(ESubmissionStatus.Duplicate)]
    public async Task LateAiDraft_PreservesTheDecisionAndHistory(ESubmissionStatus decision)
    {
        var seeded = await SeedAsync();
        var draft = (await Repository.GetByIdAsync(seeded.Id)).Data!;
        var moderated = (await Repository.GetByIdAsync(seeded.Id)).Data!;
        moderated.Status = decision;
        moderated.ModeratedBy = "moderator";
        moderated.ModeratedAt = DateTime.UtcNow;
        moderated.ApprovedJobId = decision == ESubmissionStatus.Approved ? Id.Create(Guid.NewGuid()) : null;
        moderated.History.Add(new JobSubmissionHistory
        {
            Id = Id.Create(Guid.NewGuid()), SubmissionId = seeded.Id, Status = decision, ActorId = "moderator", TenantId = "default"
        });
        (await Repository.UpdateAsync(moderated)).Status.Should().Be(EResultStatus.Success);

        draft.AiDraftJson = "{\"draft\":true}";
        draft.AiDraftProvider = "test";
        draft.AiDraftEvidenceHash = "evidence-hash";
        draft.AiDraftAt = DateTime.UtcNow;
        (await Repository.SaveAiDraftAsync(draft)).Status.Should().Be(EResultStatus.Success);

        var saved = (await Repository.GetByIdAsync(seeded.Id)).Data!;
        saved.Status.Should().Be(decision);
        saved.ModeratedBy.Should().Be("moderator");
        saved.ModeratedAt.Should().Be(moderated.ModeratedAt);
        saved.ApprovedJobId.Should().Be(moderated.ApprovedJobId);
        saved.History.Should().ContainSingle(h => h.Status == decision);
        saved.AiDraftJson.Should().Be(draft.AiDraftJson);
        saved.AiDraftEvidenceHash.Should().Be(draft.AiDraftEvidenceHash);
        saved.AiDraftProvider.Should().Be("test");
        saved.AiDraftAt.Should().Be(draft.AiDraftAt);
    }

    [Fact]
    public async Task LateDecision_PreservesTheNewAiDraft_AndAnotherDecisionIsRefused()
    {
        var seeded = await SeedAsync();
        var decision = (await Repository.GetByIdAsync(seeded.Id)).Data!;
        var draft = (await Repository.GetByIdAsync(seeded.Id)).Data!;
        draft.AiDraftJson = "{\"new\":true}";
        (await Repository.SaveAiDraftAsync(draft)).Status.Should().Be(EResultStatus.Success);
        decision.Status = ESubmissionStatus.Approved;
        (await Repository.UpdateAsync(decision)).Status.Should().Be(EResultStatus.Success);

        decision.Status = ESubmissionStatus.Rejected;
        (await Repository.UpdateAsync(decision)).Status.Should().Be(EResultStatus.DomainError);

        var saved = (await Repository.GetByIdAsync(seeded.Id)).Data!;
        saved.Status.Should().Be(ESubmissionStatus.Approved);
        saved.AiDraftJson.Should().Be(draft.AiDraftJson);
    }
}
