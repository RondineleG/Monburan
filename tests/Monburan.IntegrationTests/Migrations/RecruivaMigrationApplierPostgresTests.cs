using System.Security.Cryptography;
using System.Text;

using FluentAssertions;

using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

using Monburan.Core.Entities;
using Monburan.Core.Enums;
using Monburan.Core.UseCases.Migrations;
using Monburan.Core.ValueObjects;
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
        var identity = await context.Users.SingleAsync();
        identity.Id.Should().Be(applied.IdMap["user:user-1"]);
        identity.Email.Should().Be("cand-mig@teste.com");
        identity.EmailConfirmed.Should().BeTrue();
        identity.Email.Should().Be((await context.Candidates.SingleAsync()).Email);
        var token = await context.Set<IdentityUserToken<Guid>>().SingleAsync();
        token.LoginProvider.Should().Be("[AspNetUserStore]");
        token.Name.Should().Be("AuthenticatorKey");
        token.Value.Should().Be("secret-fixture");
        (await context.Candidates.CountAsync()).Should().Be(1);
        (await context.Advertisers.CountAsync()).Should().Be(1);
        (await context.Jobs.CountAsync()).Should().Be(1);
        (await context.Resumes.CountAsync()).Should().Be(1);
        (await context.Applications.CountAsync()).Should().Be(1);
        (await context.ApplicationStatusHistory.CountAsync()).Should().Be(1);
        applied.Files.Should().ContainSingle(file => file.Copied && File.Exists(file.TargetPath));
        (await context.Jobs.SingleAsync()).PublicationType.Should().Be(EPublicationType.Direct);
        (await context.Jobs.SingleAsync()).ApplicationChannel.Should().Be(EApplicationChannel.InternalPlatform);
        var blockedRollback = async () => await RecruivaMigrationApplier.RollbackAsync(context, applied with { Applied = false });
        await blockedRollback.Should().ThrowAsync<InvalidOperationException>();
        context.SavedJobs.Add(new SavedJob { Id = Id.Create(Guid.NewGuid()), CandidateId = Id.Create(applied.IdMap["candidate:cand-1"]), JobId = Id.Create(applied.IdMap["job:job-1"]), TenantId = "default" });
        await context.SaveChangesAsync();
        var dependentRollback = async () => await RecruivaMigrationApplier.RollbackAsync(context, applied);
        await dependentRollback.Should().ThrowAsync<InvalidOperationException>();
        context.SavedJobs.RemoveRange(context.SavedJobs);
        await context.SaveChangesAsync();

        var wrongDatabaseRollback = async () => await RecruivaMigrationApplier.RollbackAsync(context, applied with { DatabaseKey = "outro-host|outro-banco" });
        await wrongDatabaseRollback.Should().ThrowAsync<InvalidOperationException>();

        var rollback = await RecruivaMigrationApplier.RollbackAsync(context, applied);

        rollback.RemovedCounts.Values.Sum().Should().Be(7);
        rollback.RemovedFiles.Should().Be(1);
        (await context.Users.CountAsync()).Should().Be(0);
        (await context.Set<IdentityUserToken<Guid>>().CountAsync()).Should().Be(0);
        (await context.Candidates.CountAsync()).Should().Be(0);
        (await context.Applications.CountAsync()).Should().Be(0);
        applied.Files.Should().OnlyContain(file => !File.Exists(file.TargetPath));

        var leftover = applied.Files.Single(file => file.Copied);
        Directory.CreateDirectory(Path.GetDirectoryName(leftover.TargetPath)!);
        await File.WriteAllTextAsync(leftover.TargetPath, "resto de falha parcial");
        var retry = await RecruivaMigrationApplier.RollbackAsync(context, applied);
        retry.RemovedCounts.Values.Sum().Should().Be(0);
        retry.RemovedFiles.Should().Be(1);
        File.Exists(leftover.TargetPath).Should().BeFalse();
    }

    private static RecruivaMigrationExport Export(string hash, int size) => new()
    {
        TenantId = "default",
        Users = [new() { SourceId = "user-1", UserName = "cand-mig@teste.com", NormalizedUserName = "CAND-MIG@TESTE.COM", Email = "cand-mig@teste.com", NormalizedEmail = "CAND-MIG@TESTE.COM", EmailConfirmed = true, PasswordHash = "hash-fixture", SecurityStamp = Guid.NewGuid().ToString("N"), ConcurrencyStamp = Guid.NewGuid().ToString("N"), FirstName = "Candidata", LastName = "Fixture", IsActive = true, CanMigrate = true, CreatedAtUtc = DateTime.UtcNow, Tokens = [new() { LoginProvider = "[AspNetUserStore]", Name = "AuthenticatorKey", Value = "secret-fixture" }] }],
        Candidates = [new() { SourceId = "cand-1", Email = "cand-mig@teste.com", Name = "Candidata", Status = "Active", CanMigrate = true, CreatedAtUtc = DateTime.UtcNow }],
        Advertisers = [new() { SourceId = "adv-1", Email = "adv-mig@teste.com", Name = "Empresa", Status = "Active", CanMigrate = true, CreatedAtUtc = DateTime.UtcNow }],
        Jobs = [new() { SourceId = "job-1", AdvertiserSourceId = "adv-1", Title = "Vaga", Status = "Active", Description = "Descrição", ExpirationDateUtc = DateTime.UtcNow.AddDays(30), CreatedAtUtc = DateTime.UtcNow }],
        Resumes = [new() { SourceId = "res-1", CandidateSourceId = "cand-1", Title = "Currículo", Summary = "Resumo", IsActive = true, CreatedAtUtc = DateTime.UtcNow }],
        Files = [new() { SourceId = "file-1", OwnerType = "advertiser", OwnerSourceId = "adv-1", SourceUrl = "/uploads/logo.png", ContentHash = hash, SizeBytes = size, CanMigrate = true }],
        Applications = [new() { SourceId = "app-1", CandidateSourceId = "cand-1", JobSourceId = "job-1", ResumeSourceId = "res-1", Status = "Sent", AppliedAtUtc = DateTime.UtcNow, CreatedAtUtc = DateTime.UtcNow }],
        Transitions = [new() { SourceId = "tr-1", ApplicationSourceId = "app-1", Status = "Sent", OccurredAtUtc = DateTime.UtcNow, CreatedAtUtc = DateTime.UtcNow }]
    };
}
