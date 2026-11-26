using System.Data;
using System.Security.Cryptography;
using System.Text;

using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

using Monburan.Core.Entities;
using Monburan.Core.Entities.Base;
using Monburan.Core.Enums;
using Monburan.Core.UseCases.Migrations;
using Monburan.Core.ValueObjects;
using Monburan.Web.Data;
using Monburan.Web.Models;

namespace Monburan.RecruivaMigration;

public static class RecruivaMigrationApplier
{
    public static async Task<RecruivaMigrationApplyReport> ApplyAsync(
        ApplicationDbContext context,
        RecruivaMigrationExport export,
        RecruivaMigrationDryRunReport dryRun,
        string uploadsSource,
        string uploadsTarget,
        Guid? batchId = null,
        CancellationToken cancellationToken = default)
    {
        var batch = batchId ?? Guid.NewGuid();
        var marker = $"migration:recruiva:{batch:D}";
        var problems = dryRun.Problems.ToList();
        problems.AddRange(ValidateApplyShape(export));
        var files = new List<RecruivaMigrationFileResult>();
        if (problems.Count == 0)
        {
            problems.AddRange(await TargetCollisionProblemsAsync(context, export, dryRun, cancellationToken));
            files.AddRange(await CopyFilesAsync(export, batch, uploadsSource, uploadsTarget, problems, cancellationToken));
        }
        if (problems.Count != 0)
        {
            CleanupNewFiles(files);
            return Report(batch, false, dryRun, problems, files);
        }

        await using var transaction = context.Database.IsRelational() ? await context.Database.BeginTransactionAsync(cancellationToken) : null;
        try
        {
            var logoByOwner = files.Where(file => file.OwnerType == "advertiser")
                .GroupBy(file => file.OwnerSourceId).ToDictionary(group => group.Key, group => group.OrderBy(file => file.SourceId).First().TargetUrl);
            var profileEmails = export.Candidates.Select(row => row.Email.Trim())
                .Concat(export.Advertisers.Select(row => row.Email.Trim()))
                .GroupBy(email => email, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key.ToUpperInvariant(), group => group.First().Trim(), StringComparer.Ordinal);
            context.Users.AddRange(export.Users.Select(row => new ApplicationUser
            {
                Id = dryRun.IdMap[$"user:{row.SourceId}"], UserName = row.UserName.Trim(), NormalizedUserName = row.NormalizedUserName ?? row.UserName.Trim().ToUpperInvariant(),
                Email = profileEmails.GetValueOrDefault(row.Email.Trim().ToUpperInvariant()) ?? row.Email.Trim(), NormalizedEmail = row.NormalizedEmail ?? row.Email.Trim().ToUpperInvariant(), EmailConfirmed = row.EmailConfirmed,
                PasswordHash = row.PasswordHash, SecurityStamp = row.SecurityStamp, ConcurrencyStamp = row.ConcurrencyStamp,
                PhoneNumber = row.PhoneNumber, PhoneNumberConfirmed = row.PhoneNumberConfirmed, TwoFactorEnabled = row.TwoFactorEnabled,
                LockoutEnd = row.LockoutEndUtc, LockoutEnabled = row.LockoutEnabled, AccessFailedCount = row.AccessFailedCount,
                FirstName = row.FirstName, LastName = row.LastName, IsActive = row.IsActive, ProfilePicture = row.ProfilePicture,
                CreatedAt = row.CreatedAtUtc, LastLoginAt = row.LastLoginAtUtc, MigrationBatchId = batch
            }));
            context.Set<IdentityUserToken<Guid>>().AddRange(export.Users.SelectMany(row => row.Tokens.Select(token => new IdentityUserToken<Guid>
            {
                UserId = dryRun.IdMap[$"user:{row.SourceId}"], LoginProvider = token.LoginProvider, Name = token.Name, Value = token.Value
            })));
            context.Candidates.AddRange(export.Candidates.Select(row => Audit(new Candidate
            {
                Id = Id.Create(dryRun.IdMap[$"candidate:{row.SourceId}"]), TenantId = export.TenantId, CreatedBy = marker,
                Name = row.Name, Email = row.Email, Status = Parse<EAccountStatus>(row.Status, EAccountStatus.Active), IsDeleted = row.IsDeleted ?? false
            }, row.CreatedAtUtc, row.UpdatedAtUtc)));
            context.Advertisers.AddRange(export.Advertisers.Select(row => Audit(new Advertiser
            {
                Id = Id.Create(dryRun.IdMap[$"advertiser:{row.SourceId}"]), TenantId = export.TenantId, CreatedBy = marker,
                Name = row.Name, Email = row.Email, Status = Parse<EAdvertiserStatus>(row.Status, EAdvertiserStatus.Active),
                LogoUrl = logoByOwner.GetValueOrDefault(row.SourceId) ?? row.LogoUrl, IsDeleted = row.IsDeleted ?? false
            }, row.CreatedAtUtc, row.UpdatedAtUtc)));
            context.Resumes.AddRange(export.Resumes.Select(row => Audit(new Resume
            {
                Id = Id.Create(dryRun.IdMap[$"resume:{row.SourceId}"]), TenantId = export.TenantId, CreatedBy = marker,
                CandidateId = Id.Create(dryRun.IdMap[$"candidate:{row.CandidateSourceId}"]), Title = row.Title,
                Summary = row.Summary, IsActive = row.IsActive ?? true, SourceFileName = row.SourceFileName, SourceFileUrl = row.SourceFileUrl, IsDeleted = row.IsDeleted ?? false
            }, row.CreatedAtUtc, row.UpdatedAtUtc)));
            context.Jobs.AddRange(export.Jobs.Select(row => Audit(new Job
            {
                Id = Id.Create(dryRun.IdMap[$"job:{row.SourceId}"]), TenantId = export.TenantId, CreatedBy = marker,
                AdvertiserId = Id.Create(dryRun.IdMap[$"advertiser:{row.AdvertiserSourceId}"]), Title = row.Title,
                Description = row.Description!, ExpirationDate = Utc(row.ExpirationDateUtc!.Value),
                Status = Parse<EJobStatus>(row.Status, EJobStatus.Active), PublicationType = EPublicationType.Direct, ApplicationChannel = EApplicationChannel.InternalPlatform, SourceName = "Recruiva", IsDeleted = row.IsDeleted ?? false
            }, row.CreatedAtUtc, row.UpdatedAtUtc)));
            context.Applications.AddRange(export.Applications.Select(row => Audit(new Application
            {
                Id = Id.Create(dryRun.IdMap[$"application:{row.SourceId}"]), TenantId = export.TenantId, CreatedBy = marker,
                CandidateId = Id.Create(dryRun.IdMap[$"candidate:{row.CandidateSourceId}"]), JobId = Id.Create(dryRun.IdMap[$"job:{row.JobSourceId}"]),
                ResumeId = row.ResumeSourceId is null ? null : dryRun.IdMap[$"resume:{row.ResumeSourceId}"], Status = Parse<EApplicationStatus>(row.Status, EApplicationStatus.Sent),
                Notes = row.Notes, AppliedAt = row.AppliedAtUtc is null ? null : Utc(row.AppliedAtUtc.Value),
                ViewedAt = row.ViewedAtUtc is null ? null : Utc(row.ViewedAtUtc.Value), SelectedAt = row.SelectedAtUtc is null ? null : Utc(row.SelectedAtUtc.Value),
                RejectedAt = row.RejectedAtUtc is null ? null : Utc(row.RejectedAtUtc.Value), IsDeleted = row.IsDeleted ?? false
            }, row.CreatedAtUtc, row.UpdatedAtUtc)));
            context.ApplicationStatusHistory.AddRange(export.Transitions.Select(row => Audit(new ApplicationStatusHistory
            {
                Id = Id.Create(dryRun.IdMap[$"transition:{row.SourceId}"]), TenantId = export.TenantId, CreatedBy = marker,
                ApplicationId = Id.Create(dryRun.IdMap[$"application:{row.ApplicationSourceId}"]), Status = Parse<EApplicationStatus>(row.Status, EApplicationStatus.Sent),
                Date = Utc(row.OccurredAtUtc), Note = row.Note, Responsible = row.Responsible, IsDeleted = row.IsDeleted ?? false
            }, row.CreatedAtUtc, null)));
            await context.SaveChangesAsync(cancellationToken);
            if (transaction is not null) await transaction.CommitAsync(cancellationToken);
            return Report(batch, true, dryRun, [], files);
        }
        catch
        {
            if (transaction is not null) await transaction.RollbackAsync(cancellationToken);
            CleanupNewFiles(files);
            throw;
        }
    }

    public static async Task<RecruivaMigrationRollbackReport> RollbackAsync(ApplicationDbContext context, RecruivaMigrationApplyReport applyReport, CancellationToken cancellationToken = default)
    {
        if (!applyReport.Applied)
            throw new InvalidOperationException("Relatório não representa um lote aplicado; rollback recusado.");
        await using var transaction = context.Database.IsRelational() ? await context.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken) : null;
        await GuardPostMigrationDependenciesAsync(context, applyReport, cancellationToken);
        var removed = new Dictionary<string, int>();
        removed["transition"] = await DeleteRowsAsync(context, "ApplicationStatusHistory", Ids(applyReport, "transition"), $"migration:recruiva:{applyReport.BatchId:D}", cancellationToken);
        removed["application"] = await DeleteRowsAsync(context, "Applications", Ids(applyReport, "application"), $"migration:recruiva:{applyReport.BatchId:D}", cancellationToken);
        removed["resume"] = await DeleteRowsAsync(context, "Resumes", Ids(applyReport, "resume"), $"migration:recruiva:{applyReport.BatchId:D}", cancellationToken);
        removed["job"] = await DeleteRowsAsync(context, "Jobs", Ids(applyReport, "job"), $"migration:recruiva:{applyReport.BatchId:D}", cancellationToken);
        removed["advertiser"] = await DeleteRowsAsync(context, "Advertisers", Ids(applyReport, "advertiser"), $"migration:recruiva:{applyReport.BatchId:D}", cancellationToken);
        removed["candidate"] = await DeleteRowsAsync(context, "Candidates", Ids(applyReport, "candidate"), $"migration:recruiva:{applyReport.BatchId:D}", cancellationToken);
        removed["user"] = await DeleteUsersAsync(context, Ids(applyReport, "user"), applyReport.BatchId, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
        if (transaction is not null) await transaction.CommitAsync(cancellationToken);
        var files = 0;
        if (removed.Values.Sum() == 0)
            return new RecruivaMigrationRollbackReport { BatchId = applyReport.BatchId, RemovedCounts = removed, RemovedFiles = files };
        foreach (var file in applyReport.Files.Where(file => file.Copied))
        {
            if (File.Exists(file.TargetPath)) { File.Delete(file.TargetPath); files++; }
        }
        return new RecruivaMigrationRollbackReport { BatchId = applyReport.BatchId, RemovedCounts = removed, RemovedFiles = files };
    }

    private static async Task GuardPostMigrationDependenciesAsync(ApplicationDbContext context, RecruivaMigrationApplyReport report, CancellationToken cancellationToken)
    {
        var candidateIds = Ids(report, "candidate").ToHashSet();
        var jobIds = Ids(report, "job").ToHashSet();
        var marker = $"migration:recruiva:{report.BatchId:D}";
        var saved = await context.SavedJobs.Select(row => new { row.CandidateId, row.JobId }).ToListAsync(cancellationToken);
        if (saved.Any(row => candidateIds.Contains(row.CandidateId.Value) || jobIds.Contains(row.JobId.Value)))
            throw new InvalidOperationException("Rollback bloqueado: há favoritos criados após o corte; remova ou reconcile esses dependentes primeiro.");
        var applications = await context.Applications.Select(row => new { row.CandidateId, row.JobId, row.CreatedBy }).ToListAsync(cancellationToken);
        if (applications.Any(row => row.CreatedBy != marker && (candidateIds.Contains(row.CandidateId.Value) || jobIds.Contains(row.JobId.Value))))
            throw new InvalidOperationException("Rollback bloqueado: há candidaturas criadas após o corte; remova ou reconcile esses dependentes primeiro.");
    }

    private static List<Guid> Ids(RecruivaMigrationApplyReport report, string entity) => [.. report.IdMap.Where(pair => pair.Key.StartsWith(entity + ":", StringComparison.Ordinal)).Select(pair => pair.Value)];

    private static async Task<int> DeleteUsersAsync(ApplicationDbContext context, List<Guid> ids, Guid batch, CancellationToken cancellationToken)
    {
        if (ids.Count == 0) return 0;
        return await context.Database.ExecuteSqlRawAsync("DELETE FROM \"Identity\".\"Users\" WHERE \"Id\" = ANY({0}) AND \"MigrationBatchId\" = {1}", [ids.ToArray(), batch], cancellationToken);
    }

    private static async Task<int> DeleteRowsAsync(ApplicationDbContext context, string table, List<Guid> ids, string marker, CancellationToken cancellationToken)
    {
        if (ids.Count == 0) return 0;
        var sql = table switch
        {
            "ApplicationStatusHistory" => "DELETE FROM \"ApplicationStatusHistory\" WHERE \"Id\" = ANY({0}) AND \"CreatedBy\" = {1}",
            "Applications" => "DELETE FROM \"Applications\" WHERE \"Id\" = ANY({0}) AND \"CreatedBy\" = {1}",
            "Resumes" => "DELETE FROM \"Resumes\" WHERE \"Id\" = ANY({0}) AND \"CreatedBy\" = {1}",
            "Jobs" => "DELETE FROM \"Jobs\" WHERE \"Id\" = ANY({0}) AND \"CreatedBy\" = {1}",
            "Advertisers" => "DELETE FROM \"Advertisers\" WHERE \"Id\" = ANY({0}) AND \"CreatedBy\" = {1}",
            "Candidates" => "DELETE FROM \"Candidates\" WHERE \"Id\" = ANY({0}) AND \"CreatedBy\" = {1}",
            _ => throw new ArgumentOutOfRangeException(nameof(table))
        };
        return await context.Database.ExecuteSqlRawAsync(sql, [ids.ToArray(), marker], cancellationToken);
    }

    private static IEnumerable<RecruivaMigrationProblem> ValidateApplyShape(RecruivaMigrationExport export)
    {
        foreach (var job in export.Jobs.Where(job => string.IsNullOrWhiteSpace(job.Description) || job.ExpirationDateUtc is null))
            yield return new("collision", "job", job.SourceId, "Vaga sem descrição ou expiração não pode ser cortada com segurança.");
        foreach (var row in export.Files.Where(file => string.IsNullOrWhiteSpace(file.SourceUrl)))
            yield return new("orphan", "file", row.SourceId, "Arquivo sem URL de origem não pode ser copiado.");
    }

    private static async Task<List<RecruivaMigrationProblem>> TargetCollisionProblemsAsync(ApplicationDbContext context, RecruivaMigrationExport export, RecruivaMigrationDryRunReport report, CancellationToken cancellationToken)
    {
        var problems = new List<RecruivaMigrationProblem>();
        foreach (var user in export.Users)
            if (await context.Users.AnyAsync(row => row.NormalizedEmail == user.NormalizedEmail || row.Email == user.Email, cancellationToken)) problems.Add(new("collision", "user", user.SourceId, "E-mail de conta já existe no destino."));
        foreach (var candidate in export.Candidates)
            if (await context.Candidates.AnyAsync(row => row.Email == candidate.Email, cancellationToken)) problems.Add(new("collision", "candidate", candidate.SourceId, "E-mail já existe no destino."));
        foreach (var advertiser in export.Advertisers)
            if (await context.Advertisers.AnyAsync(row => row.Email == advertiser.Email, cancellationToken)) problems.Add(new("collision", "advertiser", advertiser.SourceId, "E-mail já existe no destino."));
        var ids = report.IdMap.Values.ToHashSet();
        var existing = new HashSet<Guid>();
        foreach (var id in await context.Users.Select(row => row.Id).ToListAsync(cancellationToken)) if (ids.Contains(id)) existing.Add(id);
        foreach (var id in await context.Candidates.Select(row => row.Id).ToListAsync(cancellationToken)) if (ids.Contains(id.Value)) existing.Add(id.Value);
        foreach (var id in await context.Advertisers.Select(row => row.Id).ToListAsync(cancellationToken)) if (ids.Contains(id.Value)) existing.Add(id.Value);
        foreach (var id in await context.Jobs.Select(row => row.Id).ToListAsync(cancellationToken)) if (ids.Contains(id.Value)) existing.Add(id.Value);
        foreach (var id in await context.Applications.Select(row => row.Id).ToListAsync(cancellationToken)) if (ids.Contains(id.Value)) existing.Add(id.Value);
        if (existing.Count != 0)
            problems.Add(new("collision", "id-map", string.Empty, "ID determinístico já existe no destino; use rollback do lote antes de repetir."));
        return problems;
    }

    private static async Task<List<RecruivaMigrationFileResult>> CopyFilesAsync(RecruivaMigrationExport export, Guid batch, string sourceRoot, string targetRoot, List<RecruivaMigrationProblem> problems, CancellationToken cancellationToken)
    {
        var results = new List<RecruivaMigrationFileResult>();
        foreach (var file in export.Files)
        {
            var relativeSource = file.SourceUrl!.Split('?', '#')[0].TrimStart('/')["uploads/".Length..];
            var source = SafePath(sourceRoot, relativeSource);
            var extension = Path.GetExtension(relativeSource);
            var relativeTarget = $"recruiva/{batch:D}/{file.SourceId}{extension}";
            var target = SafePath(targetRoot, relativeTarget);
            if (source is null || target is null || !File.Exists(source)) { problems.Add(new("collision", "file", file.SourceId, "Arquivo físico ausente ou fora da raiz autorizada.")); continue; }
            await using var input = File.OpenRead(source);
            var hash = Convert.ToHexString(await SHA256.HashDataAsync(input, cancellationToken)).ToLowerInvariant();
            if (hash != file.ContentHash || new FileInfo(source).Length != file.SizeBytes) { problems.Add(new("collision", "file", file.SourceId, "Hash/tamanho físico diverge da exportação.")); continue; }
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            var copied = false;
            if (File.Exists(target))
            {
                await using var existing = File.OpenRead(target);
                var existingHash = Convert.ToHexString(await SHA256.HashDataAsync(existing, cancellationToken)).ToLowerInvariant();
                if (existingHash != file.ContentHash || existing.Length != file.SizeBytes)
                {
                    problems.Add(new("collision", "file", file.SourceId, "Arquivo de destino já existe com conteúdo divergente."));
                    continue;
                }
            }
            else { File.Copy(source, target); copied = true; }
            results.Add(new(file.SourceId, file.OwnerType, file.OwnerSourceId, source, target, $"/uploads/{relativeTarget.Replace('\\', '/')}", copied));
        }
        return results;
    }

    private static string? SafePath(string root, string relative)
    {
        var full = Path.GetFullPath(Path.Combine(root, relative));
        var safeRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        return full.StartsWith(safeRoot, StringComparison.Ordinal) ? full : null;
    }

    private static void CleanupNewFiles(IEnumerable<RecruivaMigrationFileResult> files)
    {
        foreach (var file in files.Where(file => file.Copied && File.Exists(file.TargetPath))) File.Delete(file.TargetPath);
    }

    private static T Audit<T>(T entity, DateTime? created, DateTime? updated) where T : BaseEntity
    {
        if (created.HasValue) typeof(BaseEntity).GetProperty(nameof(BaseEntity.CreatedAt))!.SetValue(entity, Utc(created.Value));
        if (updated.HasValue) typeof(BaseEntity).GetProperty(nameof(BaseEntity.UpdatedAt))!.SetValue(entity, Utc(updated.Value));
        return entity;
    }

    private static TEnum Parse<TEnum>(string? value, TEnum fallback) where TEnum : struct => Enum.TryParse<TEnum>(value, true, out var parsed) ? parsed : fallback;
    private static DateTime Utc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
    };
    private static RecruivaMigrationApplyReport Report(Guid batch, bool applied, RecruivaMigrationDryRunReport dryRun, IReadOnlyList<RecruivaMigrationProblem> problems, IReadOnlyList<RecruivaMigrationFileResult> files) => new()
    { BatchId = batch, Applied = applied, Version = dryRun.Version, Counts = dryRun.Counts, Hashes = dryRun.Hashes, IdMap = dryRun.IdMap, Problems = problems, Files = files };
}

public sealed record RecruivaMigrationFileResult(string SourceId, string OwnerType, string OwnerSourceId, string SourcePath, string TargetPath, string TargetUrl, bool Copied);
public sealed record RecruivaMigrationApplyReport
{
    public required Guid BatchId { get; init; }
    public required bool Applied { get; init; }
    public required string Version { get; init; }
    public required IReadOnlyDictionary<string, int> Counts { get; init; }
    public required IReadOnlyDictionary<string, string> Hashes { get; init; }
    public required IReadOnlyDictionary<string, Guid> IdMap { get; init; }
    public required IReadOnlyList<RecruivaMigrationProblem> Problems { get; init; }
    public required IReadOnlyList<RecruivaMigrationFileResult> Files { get; init; }
}
public sealed record RecruivaMigrationRollbackReport
{
    public required Guid BatchId { get; init; }
    public required IReadOnlyDictionary<string, int> RemovedCounts { get; init; }
    public required int RemovedFiles { get; init; }
}
