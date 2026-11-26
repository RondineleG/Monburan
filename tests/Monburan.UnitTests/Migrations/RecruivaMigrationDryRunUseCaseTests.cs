using FluentAssertions;

using Monburan.Core.Enums;
using Monburan.Core.UseCases.Migrations;

using Xunit;

namespace Monburan.UnitTests.Migrations;

public sealed class RecruivaMigrationDryRunUseCaseTests
{
    private readonly BuildRecruivaMigrationDryRunUseCase _useCase = new();

    [Fact]
    public async Task ValidExport_ReconcilesCountsDeterministicHashesAndIdMap()
    {
        var export = ValidExport();

        var first = await _useCase.ExecuteAsync(export);
        var second = await _useCase.ExecuteAsync(export);

        first.Status.Should().Be(EResultStatus.Success);
        first.Data!.CanConfirm.Should().BeTrue();
        first.Data.Problems.Should().BeEmpty();
        first.Data.Version.Should().Be(BuildRecruivaMigrationDryRunUseCase.MapVersion);
        first.Data.Counts.Should().Contain("candidate", 1).And.Contain("job", 1).And.Contain("application", 1).And.Contain("transition", 1);
        first.Data.Hashes.Should().BeEquivalentTo(second.Data!.Hashes, "a mesma exportação precisa reconciliar o mesmo hash");
        first.Data.IdMap.Should().BeEquivalentTo(second.Data.IdMap, "o mapa de IDs precisa ser reproduzível para rollback/reconciliação");
        first.Data.IdMap.Keys.Should().Contain(["candidate:cand-1", "job:job-1", "application:app-1"]);
    }

    [Fact]
    public async Task InvalidReferencesPermissionsAndFiles_BlockConfirmationWithoutTouchingTheExport()
    {
        var export = ValidExport() with
        {
            Candidates = [new RecruivaCandidateExport { SourceId = "cand-1", Email = "cand@teste.com", Name = "Candidata", CanMigrate = false }],
            Jobs = [new RecruivaJobExport { SourceId = "job-1", AdvertiserSourceId = "empresa-sumida", Title = "Vaga", Status = "Active" }],
            Files = [new RecruivaFileExport { SourceId = "file-1", OwnerType = "candidate", OwnerSourceId = "dono-sumido", ContentHash = "", SizeBytes = 0, CanMigrate = true }]
        };
        var originalCounts = export.Counts();

        var result = await _useCase.ExecuteAsync(export);

        result.Data!.CanConfirm.Should().BeFalse();
        result.Data.Problems.Select(problem => problem.Kind).Should().Contain(["permission", "orphan", "collision"]);
        export.Counts().Should().Be(originalCounts, "dry-run é somente leitura e não altera a exportação de origem");
    }

    [Fact]
    public async Task AdvertiserOwnedFile_IsAcceptedAsMigratableAttachment()
    {
        var export = ValidExport() with
        {
            Files = [new RecruivaFileExport { SourceId = "file-adv", OwnerType = "advertiser", OwnerSourceId = "adv-1", SourceUrl = "/uploads/logo.png", ContentHash = "abc123", SizeBytes = 10, CanMigrate = true }]
        };

        var result = await _useCase.ExecuteAsync(export);

        result.Data!.CanConfirm.Should().BeTrue();
        result.Data.Problems.Should().BeEmpty();
    }

    [Fact]
    public async Task DuplicateNaturalKeys_AreReportedAsCollisions()
    {
        var valid = ValidExport();
        var export = valid with
        {
            Candidates =
            [
                new RecruivaCandidateExport { SourceId = "cand-1", Email = "cand@teste.com", Name = "Uma", CanMigrate = true },
                new RecruivaCandidateExport { SourceId = "cand-2", Email = "CAND@teste.com", Name = "Outra", CanMigrate = true }
            ],
            Applications =
            [
                valid.Applications[0],
                valid.Applications[0] with { SourceId = "app-2" }
            ]
        };

        var result = await _useCase.ExecuteAsync(export);

        result.Data!.CanConfirm.Should().BeFalse();
        result.Data.Problems.Should().Contain(problem => problem.Kind == "collision" && problem.Entity == "candidate");
        result.Data.Problems.Should().Contain(problem => problem.Kind == "collision" && problem.Entity == "application");
    }

    [Fact]
    public async Task DuplicateTransitionIds_MissingPermissionAndForeignResume_BlockConfirmation()
    {
        var valid = ValidExport();
        var foreignResume = valid.Resumes[0] with { CandidateSourceId = "outro-candidato" };
        var export = valid with
        {
            Candidates = [valid.Candidates[0] with { CanMigrate = null }],
            Applications = [valid.Applications[0] with { ResumeSourceId = "res-1" }],
            Resumes = [foreignResume],
            Transitions = [valid.Transitions[0], valid.Transitions[0] with { OccurredAtUtc = valid.Transitions[0].OccurredAtUtc.AddMinutes(1) }]
        };

        var result = await _useCase.ExecuteAsync(export);

        result.Data!.CanConfirm.Should().BeFalse();
        result.Data.Problems.Should().Contain(problem => problem.Kind == "collision" && problem.Entity == "transition");
        result.Data.Problems.Should().Contain(problem => problem.Kind == "permission" && problem.Entity == "candidate");
        result.Data.Problems.Should().Contain(problem => problem.Kind == "collision" && problem.Entity == "application");
    }

    [Fact]
    public async Task IdMap_UsesUuidVersionFiveInTextForm()
    {
        var result = await _useCase.ExecuteAsync(ValidExport());

        result.Data!.IdMap.Values.Should().OnlyContain(id => id.ToString("D")[14] == '5');
    }

    [Fact]
    public async Task EquivalentTimestampsInDifferentKinds_ProduceTheSameHash()
    {
        var utc = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        var local = utc.ToLocalTime();
        var first = ValidExport() with { Candidates = [ValidExport().Candidates[0] with { CreatedAtUtc = utc }] };
        var second = ValidExport() with { Candidates = [ValidExport().Candidates[0] with { CreatedAtUtc = local }] };

        var firstResult = await _useCase.ExecuteAsync(first);
        var secondResult = await _useCase.ExecuteAsync(second);

        firstResult.Data!.Hashes["candidate"].Should().Be(secondResult.Data!.Hashes["candidate"]);
    }

    private static RecruivaMigrationExport ValidExport() => new()
    {
        TenantId = "default",
        Candidates = [new RecruivaCandidateExport { SourceId = "cand-1", Email = "cand@teste.com", Name = "Candidata", CanMigrate = true }],
        Advertisers = [new RecruivaAdvertiserExport { SourceId = "adv-1", Email = "empresa@teste.com", Name = "Empresa", CanMigrate = true }],
        Jobs = [new RecruivaJobExport { SourceId = "job-1", AdvertiserSourceId = "adv-1", Title = "Vaga", Status = "Active" }],
        Resumes = [new RecruivaResumeExport { SourceId = "res-1", CandidateSourceId = "cand-1", Title = "Currículo" }],
        Files = [new RecruivaFileExport { SourceId = "file-1", OwnerType = "candidate", OwnerSourceId = "cand-1", ContentHash = "abc123", SizeBytes = 10, CanMigrate = true }],
        Applications = [new RecruivaApplicationExport { SourceId = "app-1", CandidateSourceId = "cand-1", JobSourceId = "job-1", ResumeSourceId = "res-1", Status = "Sent" }],
        Transitions = [new RecruivaTransitionExport { SourceId = "tr-1", ApplicationSourceId = "app-1", Status = "Sent", OccurredAtUtc = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) }]
    };
}

internal static class RecruivaMigrationExportTestExtensions
{
    public static int Counts(this RecruivaMigrationExport export) =>
        export.Candidates.Count + export.Advertisers.Count + export.Jobs.Count + export.Resumes.Count +
        export.Files.Count + export.Applications.Count + export.Transitions.Count;
}
