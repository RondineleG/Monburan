using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

using Monburan.Core.Entities;
using Monburan.Core.Enums;
using Monburan.Core.Interfaces.Storage;
using Monburan.Core.Services;
using Monburan.Core.ValueObjects;
using Monburan.Web.Data;
using Monburan.Web.Middleware;
using Monburan.Web.Repositories;
using Monburan.Web.Services;

namespace Monburan.ApiTests;

/// <summary>Itens do checklist de pré-produção: retenção das evidências, limite de links e token fora do log.</summary>
public sealed class PreProdHardeningTests : IClassFixture<MonburanApiFactory>
{
    private readonly MonburanApiFactory _factory;

    public PreProdHardeningTests(MonburanApiFactory factory) => _factory = factory;

    private IDbContextFactory<ApplicationDbContext> Db => _factory.Services.GetRequiredService<IDbContextFactory<ApplicationDbContext>>();

    private async Task<string> AddSubmissionAsync(ESubmissionStatus status, DateTime? moderatedAt)
    {
        var storage = _factory.Services.GetRequiredService<IEvidenceStorage>();
        var key = await storage.SaveAsync([1, 2, 3], "image/png");
        var submissionId = Id.Create(Guid.NewGuid());

        var submission = new JobSubmission
        {
            Id = submissionId,
            Title = "Vaga sugerida",
            Status = status,
            ModeratedAt = moderatedAt,
            TenantId = "default"
        };
        submission.Evidences.Add(new JobSubmissionEvidence
        {
            Id = Id.Create(Guid.NewGuid()),
            SubmissionId = submissionId,
            FileName = "cartaz.png",
            ContentType = "image/png",
            Size = 3,
            Sha256 = Guid.NewGuid().ToString("N"),
            StorageKey = key,
            TenantId = "default"
        });

        await using var db = await Db.CreateDbContextAsync();
        db.JobSubmissions.Add(submission);
        await db.SaveChangesAsync();
        return key;
    }

    [Fact]
    public async Task Retention_DeletesOnlyImagesOfOldClosedSubmissions()
    {
        var old = DateTime.UtcNow.AddDays(-120);
        var oldRejected = await AddSubmissionAsync(ESubmissionStatus.Rejected, old);
        var oldDuplicate = await AddSubmissionAsync(ESubmissionStatus.Duplicate, old);
        var recentRejected = await AddSubmissionAsync(ESubmissionStatus.Rejected, DateTime.UtcNow.AddDays(-5));
        var oldApproved = await AddSubmissionAsync(ESubmissionStatus.Approved, old);
        var pending = await AddSubmissionAsync(ESubmissionStatus.Pending, null);

        var service = new EvidenceRetentionBackgroundService(
            _factory.Services,
            new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Evidence:RetentionDays"] = "90" }).Build(),
            TimeProvider.System,
            NullLogger<EvidenceRetentionBackgroundService>.Instance);

        await service.PurgeAsync(CancellationToken.None);

        var storage = _factory.Services.GetRequiredService<IEvidenceStorage>();
        (await storage.ReadAsync(oldRejected)).Should().BeNull();
        (await storage.ReadAsync(oldDuplicate)).Should().BeNull();
        (await storage.ReadAsync(recentRejected)).Should().NotBeNull();
        (await storage.ReadAsync(oldApproved)).Should().NotBeNull();
        (await storage.ReadAsync(pending)).Should().NotBeNull();
    }

    [Fact]
    public async Task ResumeShareRepository_RefusesLinkAboveActiveLimit()
    {
        var repository = new ResumeShareRepository(Db);
        var resumeId = Id.Create(Guid.NewGuid());

        ResumeShare NewShare() => new()
        {
            Id = Id.Create(Guid.NewGuid()),
            ResumeId = resumeId,
            TokenHash = Guid.NewGuid().ToString("N"),
            TenantId = "default"
        };

        // Um revogado não conta para o limite
        var revoked = NewShare();
        revoked.RevokedAt = DateTime.UtcNow.AddMinutes(-1);
        (await repository.CreateAsync(revoked)).Status.Should().Be(EResultStatus.Success);

        for (var i = 0; i < ResumeShareRules.MaxActiveSharesPerResume; i++)
            (await repository.CreateAsync(NewShare())).Status.Should().Be(EResultStatus.Success);

        var extra = await repository.CreateAsync(NewShare());
        extra.Status.Should().NotBe(EResultStatus.Success);
    }

    [Theory]
    [InlineData("/curriculo/abc123token", "/curriculo/***")]
    [InlineData("/CURRICULO/abc", "/curriculo/***")]
    [InlineData("/curriculos", "/curriculos")]
    [InlineData("/curriculos/novo", "/curriculos/novo")]
    [InlineData("/api/v1/jobs", "/api/v1/jobs")]
    public void PathForLog_HidesPublicResumeToken(string path, string expected) =>
        RateLimiting.PathForLog(path).Should().Be(expected);
}
