using System.Security.Cryptography;
using System.Text;

using FluentAssertions;

using Microsoft.EntityFrameworkCore;

using Monburan.Core.UseCases.Migrations;
using Monburan.IntegrationTests.Infrastructure;
using Monburan.RecruivaMigration;

namespace Monburan.IntegrationTests.Migrations;

public sealed class RecruivaMigrationApplierPostgresTests : IClassFixture<PostgresDatabase>
{
    private readonly PostgresDatabase _database;
    public RecruivaMigrationApplierPostgresTests(PostgresDatabase database) => _database = database;

    [Fact]
    public async Task ApplyThenRollback_RestoresDisposableDatabaseAndUploadedFile()
    {
        await using var context = _database.CreateContext();
        await context.Database.MigrateAsync();
        var source = Directory.CreateTempSubdirectory();
        var target = Directory.CreateTempSubdirectory();
        var content = Encoding.UTF8.GetBytes("logo sintetica");
        await File.WriteAllBytesAsync(Path.Combine(source.FullName, "logo.png"), content);
        var hash = Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant();
        var export = Export(hash, content.Length);
        var dry = (await new BuildRecruivaMigrationDryRunUseCase().ExecuteAsync(export)).Data!;
        dry.CanConfirm.Should().BeTrue();

        var batch = Guid.NewGuid();
        var applied = await RecruivaMigrationApplier.ApplyAsync(context, export, dry, source.FullName, target.FullName, batch);

        applied.Applied.Should().BeTrue(string.Join(';', applied.Problems.Select(p => p.Message)));
        (await context.Candidates.CountAsync()).Should().Be(1);
        (await context.Advertisers.CountAsync()).Should().Be(1);
        (await context.Jobs.CountAsync()).Should().Be(1);
        (await context.Resumes.CountAsync()).Should().Be(1);
        (await context.Applications.CountAsync()).Should().Be(1);
        (await context.ApplicationStatusHistory.CountAsync()).Should().Be(1);
        applied.Files.Should().ContainSingle(file => file.Copied && File.Exists(file.TargetPath));

        var rollback = await RecruivaMigrationApplier.RollbackAsync(context, applied);

        rollback.RemovedCounts.Values.Sum().Should().Be(6);
        rollback.RemovedFiles.Should().Be(1);
        (await context.Candidates.CountAsync()).Should().Be(0);
        (await context.Applications.CountAsync()).Should().Be(0);
        applied.Files.Should().OnlyContain(file => !File.Exists(file.TargetPath));
    }

    private static RecruivaMigrationExport Export(string hash, int size) => new()
    {
        TenantId = "default",
        Candidates = [new() { SourceId = "cand-1", Email = "cand-mig@teste.com", Name = "Candidata", Status = "Active", CanMigrate = true, CreatedAtUtc = DateTime.UtcNow }],
        Advertisers = [new() { SourceId = "adv-1", Email = "adv-mig@teste.com", Name = "Empresa", Status = "Active", CanMigrate = true, CreatedAtUtc = DateTime.UtcNow }],
        Jobs = [new() { SourceId = "job-1", AdvertiserSourceId = "adv-1", Title = "Vaga", Status = "Active", Description = "Descrição", ExpirationDateUtc = DateTime.UtcNow.AddDays(30), CreatedAtUtc = DateTime.UtcNow }],
        Resumes = [new() { SourceId = "res-1", CandidateSourceId = "cand-1", Title = "Currículo", Summary = "Resumo", IsActive = true, CreatedAtUtc = DateTime.UtcNow }],
        Files = [new() { SourceId = "file-1", OwnerType = "advertiser", OwnerSourceId = "adv-1", SourceUrl = "/uploads/logo.png", ContentHash = hash, SizeBytes = size, CanMigrate = true }],
        Applications = [new() { SourceId = "app-1", CandidateSourceId = "cand-1", JobSourceId = "job-1", ResumeSourceId = "res-1", Status = "Sent", AppliedAtUtc = DateTime.UtcNow, CreatedAtUtc = DateTime.UtcNow }],
        Transitions = [new() { SourceId = "tr-1", ApplicationSourceId = "app-1", Status = "Sent", OccurredAtUtc = DateTime.UtcNow, CreatedAtUtc = DateTime.UtcNow }]
    };
}
