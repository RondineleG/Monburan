using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

using Monburan.Core.Interfaces.UseCases;
using Monburan.Core.Requests;

namespace Monburan.Core.UseCases.Migrations;

/// <summary>
/// Monta o dry-run da migração Recruiva sem tocar na origem nem no destino. O resultado é determinístico:
/// as mesmas entradas produzem os mesmos hashes e o mesmo mapa de IDs.
/// </summary>
public sealed class BuildRecruivaMigrationDryRunUseCase : IUseCase<RecruivaMigrationExport, RecruivaMigrationDryRunReport>
{
    public const string MapVersion = "recruiva-map-v1";

    public Task<RequestResult<RecruivaMigrationDryRunReport>> ExecuteAsync(RecruivaMigrationExport request, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var problems = new List<RecruivaMigrationProblem>();
        ValidateTenant(request, problems);

        AddDuplicateSourceIds(request.Users, "user", problems);
        AddDuplicateSourceIds(request.Candidates, "candidate", problems);
        AddDuplicateSourceIds(request.Advertisers, "advertiser", problems);
        AddDuplicateSourceIds(request.Jobs, "job", problems);
        AddDuplicateSourceIds(request.Resumes, "resume", problems);
        AddDuplicateSourceIds(request.Files, "file", problems);
        AddDuplicateSourceIds(request.Applications, "application", problems);
        AddDuplicateSourceIds(request.Transitions, "transition", problems);
        AddEmailCollisions(request, problems);
        AddPermissionProblems(request, problems);
        AddReferenceProblems(request, problems);
        AddFileProblems(request, problems);

        var report = new RecruivaMigrationDryRunReport
        {
            Version = MapVersion,
            CanConfirm = problems.Count == 0,
            Counts = new Dictionary<string, int>
            {
                ["user"] = request.Users.Count,
                ["candidate"] = request.Candidates.Count,
                ["advertiser"] = request.Advertisers.Count,
                ["job"] = request.Jobs.Count,
                ["resume"] = request.Resumes.Count,
                ["file"] = request.Files.Count,
                ["application"] = request.Applications.Count,
                ["transition"] = request.Transitions.Count
            },
            Hashes = new Dictionary<string, string>
            {
                ["user"] = HashRows(request.Users),
                ["candidate"] = HashRows(request.Candidates),
                ["advertiser"] = HashRows(request.Advertisers),
                ["job"] = HashRows(request.Jobs),
                ["resume"] = HashRows(request.Resumes),
                ["file"] = HashRows(request.Files),
                ["application"] = HashRows(request.Applications),
                ["transition"] = HashRows(request.Transitions)
            },
            IdMap = BuildIdMap(request),
            Problems = problems
        };
        return Task.FromResult(RequestResult<RecruivaMigrationDryRunReport>.Success(report));
    }

    private static void ValidateTenant(RecruivaMigrationExport request, List<RecruivaMigrationProblem> problems)
    {
        if (string.IsNullOrWhiteSpace(request.TenantId))
            problems.Add(new("permission", "export", string.Empty, "A exportação precisa informar o tenant de origem."));
    }

    private static void AddDuplicateSourceIds<T>(IReadOnlyList<T> rows, string entity, List<RecruivaMigrationProblem> problems)
        where T : RecruivaSourceRow
    {
        foreach (var group in rows.GroupBy(row => row.SourceId, StringComparer.Ordinal).Where(group => group.Count() > 1))
            problems.Add(new("collision", entity, group.Key, "ID de origem repetido na exportação."));
    }

    private static void AddEmailCollisions(RecruivaMigrationExport request, List<RecruivaMigrationProblem> problems)
    {
        foreach (var group in request.Users.Where(row => !string.IsNullOrWhiteSpace(row.Email))
                     .GroupBy(row => row.Email.Trim(), StringComparer.OrdinalIgnoreCase).Where(group => group.Count() > 1))
            problems.Add(new("collision", "user", group.Key, "E-mail de conta repetido na exportação."));
        foreach (var group in request.Candidates.Where(row => !string.IsNullOrWhiteSpace(row.Email))
                     .GroupBy(row => row.Email.Trim(), StringComparer.OrdinalIgnoreCase).Where(group => group.Count() > 1))
            problems.Add(new("collision", "candidate", group.Key, "E-mail de candidato repetido na exportação."));
        foreach (var group in request.Advertisers.Where(row => !string.IsNullOrWhiteSpace(row.Email))
                     .GroupBy(row => row.Email.Trim(), StringComparer.OrdinalIgnoreCase).Where(group => group.Count() > 1))
            problems.Add(new("collision", "advertiser", group.Key, "E-mail de empresa repetido na exportação."));
        foreach (var group in request.Applications.GroupBy(row => $"{row.CandidateSourceId}\n{row.JobSourceId}", StringComparer.Ordinal)
                     .Where(group => group.Count() > 1))
            problems.Add(new("collision", "application", group.First().SourceId, "Mais de uma candidatura para o mesmo candidato e vaga."));
    }

    private static void AddPermissionProblems(RecruivaMigrationExport request, List<RecruivaMigrationProblem> problems)
    {
        foreach (var row in request.Users.Where(row => row.CanMigrate != true))
            problems.Add(new("permission", "user", row.SourceId, "Conta sem permissão registrada para migração."));
        foreach (var row in request.Candidates.Where(row => row.CanMigrate != true))
            problems.Add(new("permission", "candidate", row.SourceId, "Candidato sem permissão registrada para migração."));
        foreach (var row in request.Advertisers.Where(row => row.CanMigrate != true))
            problems.Add(new("permission", "advertiser", row.SourceId, "Empresa sem permissão registrada para migração."));
        foreach (var row in request.Files.Where(row => row.CanMigrate != true))
            problems.Add(new("permission", "file", row.SourceId, "Arquivo sem direito de migração registrado."));
    }

    private static void AddReferenceProblems(RecruivaMigrationExport request, List<RecruivaMigrationProblem> problems)
    {
        var advertisers = request.Advertisers.Select(row => row.SourceId).ToHashSet(StringComparer.Ordinal);
        var candidates = request.Candidates.Select(row => row.SourceId).ToHashSet(StringComparer.Ordinal);
        var jobs = request.Jobs.Select(row => row.SourceId).ToHashSet(StringComparer.Ordinal);
        var resumes = request.Resumes.Select(row => row.SourceId).ToHashSet(StringComparer.Ordinal);
        var applications = request.Applications.Select(row => row.SourceId).ToHashSet(StringComparer.Ordinal);

        foreach (var job in request.Jobs.Where(job => !advertisers.Contains(job.AdvertiserSourceId)))
            problems.Add(new("orphan", "job", job.SourceId, "Vaga referencia empresa inexistente na exportação."));
        foreach (var resume in request.Resumes.Where(resume => !candidates.Contains(resume.CandidateSourceId)))
            problems.Add(new("orphan", "resume", resume.SourceId, "Currículo referencia candidato inexistente na exportação."));
        var resumesBySourceId = request.Resumes.GroupBy(resume => resume.SourceId, StringComparer.Ordinal).ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
        foreach (var application in request.Applications)
        {
            var linkedResume = !string.IsNullOrWhiteSpace(application.ResumeSourceId) && resumesBySourceId.TryGetValue(application.ResumeSourceId, out var resume) ? resume : null;
            if (linkedResume is not null && linkedResume.CandidateSourceId != application.CandidateSourceId)
                problems.Add(new("collision", "application", application.SourceId, "Candidatura referencia currículo de outro candidato."));
            if (!candidates.Contains(application.CandidateSourceId))
                problems.Add(new("orphan", "application", application.SourceId, "Candidatura referencia candidato inexistente."));
            if (!jobs.Contains(application.JobSourceId))
                problems.Add(new("orphan", "application", application.SourceId, "Candidatura referencia vaga inexistente."));
            if (!string.IsNullOrWhiteSpace(application.ResumeSourceId) && !resumes.Contains(application.ResumeSourceId))
                problems.Add(new("orphan", "application", application.SourceId, "Candidatura referencia currículo inexistente."));
        }
        var profileEmails = request.Candidates.Select(row => row.Email.Trim()).Concat(request.Advertisers.Select(row => row.Email.Trim())).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var user in request.Users)
        {
            if (!profileEmails.Contains(user.Email.Trim()))
                problems.Add(new("orphan", "user", user.SourceId, "Conta não corresponde a candidato ou empresa da exportação."));
            foreach (var token in user.Tokens.Where(token => string.IsNullOrWhiteSpace(token.LoginProvider) || string.IsNullOrWhiteSpace(token.Name)))
                problems.Add(new("collision", "user", user.SourceId, "Token de autenticação sem provedor ou nome não pode ser restaurado."));
            foreach (var group in user.Tokens.GroupBy(token => $"{token.LoginProvider}\n{token.Name}", StringComparer.Ordinal).Where(group => group.Count() > 1))
                problems.Add(new("collision", "user", user.SourceId, "Token de autenticação repetido para a mesma conta."));
        }
        foreach (var transition in request.Transitions.Where(row => !applications.Contains(row.ApplicationSourceId)))
            problems.Add(new("orphan", "transition", transition.SourceId, "Transição referencia candidatura inexistente."));
    }

    private static void AddFileProblems(RecruivaMigrationExport request, List<RecruivaMigrationProblem> problems)
    {
        var candidates = request.Candidates.Select(row => row.SourceId).ToHashSet(StringComparer.Ordinal);
        var advertisers = request.Advertisers.Select(row => row.SourceId).ToHashSet(StringComparer.Ordinal);
        var jobs = request.Jobs.Select(row => row.SourceId).ToHashSet(StringComparer.Ordinal);
        foreach (var file in request.Files)
        {
            var ownerExists = file.OwnerType switch
            {
                "candidate" => candidates.Contains(file.OwnerSourceId),
                "advertiser" => advertisers.Contains(file.OwnerSourceId),
                "job" => jobs.Contains(file.OwnerSourceId),
                _ => false
            };
            if (!ownerExists)
                problems.Add(new("orphan", "file", file.SourceId, "Arquivo referencia dono inexistente ou tipo não suportado."));
            if (string.IsNullOrWhiteSpace(file.ContentHash) || file.SizeBytes <= 0)
                problems.Add(new("collision", "file", file.SourceId, "Arquivo sem hash ou tamanho válido impede reconciliação."));
        }
    }

    private static IReadOnlyDictionary<string, Guid> BuildIdMap(RecruivaMigrationExport request)
    {
        var map = new Dictionary<string, Guid>(StringComparer.Ordinal);
        AddRows(map, request.TenantId, "user", request.Users);
        AddRows(map, request.TenantId, "candidate", request.Candidates);
        AddRows(map, request.TenantId, "advertiser", request.Advertisers);
        AddRows(map, request.TenantId, "job", request.Jobs);
        AddRows(map, request.TenantId, "resume", request.Resumes);
        AddRows(map, request.TenantId, "file", request.Files);
        AddRows(map, request.TenantId, "application", request.Applications);
        AddRows(map, request.TenantId, "transition", request.Transitions);
        return map;
    }

    private static void AddRows<T>(Dictionary<string, Guid> map, string tenant, string entity, IEnumerable<T> rows)
        where T : RecruivaSourceRow
    {
        foreach (var row in rows.OrderBy(row => row.SourceId, StringComparer.Ordinal))
            map.TryAdd($"{entity}:{row.SourceId}", DeterministicGuid(tenant, entity, row.SourceId));
    }

    private static Guid DeterministicGuid(string tenant, string entity, string sourceId)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"{MapVersion}|{tenant}|{entity}|{sourceId}"))[..16];
        bytes[7] = (byte)((bytes[7] & 0x0f) | 0x50);
        bytes[8] = (byte)((bytes[8] & 0x3f) | 0x80);
        return new Guid(bytes);
    }

    private static readonly JsonSerializerOptions HashJsonOptions = new() { Converters = { new UtcDateTimeJsonConverter() } };

    private static string HashRows<T>(IReadOnlyList<T> rows) where T : RecruivaSourceRow
    {
        var ordered = rows.OrderBy(row => row.SourceId, StringComparer.Ordinal).ToArray();
        var json = JsonSerializer.Serialize(ordered, HashJsonOptions);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(json))).ToLowerInvariant();
    }
}

file sealed class UtcDateTimeJsonConverter : JsonConverter<DateTime>
{
    public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) => reader.GetDateTime();
    public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.Kind == DateTimeKind.Unspecified ? DateTime.SpecifyKind(value, DateTimeKind.Utc) : value.ToUniversalTime());
}

public abstract record RecruivaSourceRow
{
    public required string SourceId { get; init; }
    public bool? IsDeleted { get; init; }
}

public sealed record RecruivaMigrationExport
{
    public required string TenantId { get; init; }
    public IReadOnlyList<RecruivaUserExport> Users { get; init; } = [];
    public IReadOnlyList<RecruivaCandidateExport> Candidates { get; init; } = [];
    public IReadOnlyList<RecruivaAdvertiserExport> Advertisers { get; init; } = [];
    public IReadOnlyList<RecruivaJobExport> Jobs { get; init; } = [];
    public IReadOnlyList<RecruivaResumeExport> Resumes { get; init; } = [];
    public IReadOnlyList<RecruivaFileExport> Files { get; init; } = [];
    public IReadOnlyList<RecruivaApplicationExport> Applications { get; init; } = [];
    public IReadOnlyList<RecruivaTransitionExport> Transitions { get; init; } = [];
}

public sealed record RecruivaUserExport : RecruivaSourceRow
{
    public required string UserName { get; init; }
    public string? NormalizedUserName { get; init; }
    public required string Email { get; init; }
    public string? NormalizedEmail { get; init; }
    public bool EmailConfirmed { get; init; }
    public string? PasswordHash { get; init; }
    public string? SecurityStamp { get; init; }
    public string? ConcurrencyStamp { get; init; }
    public string? PhoneNumber { get; init; }
    public bool PhoneNumberConfirmed { get; init; }
    public bool TwoFactorEnabled { get; init; }
    public DateTimeOffset? LockoutEndUtc { get; init; }
    public bool LockoutEnabled { get; init; }
    public int AccessFailedCount { get; init; }
    public string FirstName { get; init; } = string.Empty;
    public string LastName { get; init; } = string.Empty;
    public bool IsActive { get; init; } = true;
    public byte[] ProfilePicture { get; init; } = [];
    public DateTime? CreatedAtUtc { get; init; }
    public DateTime? LastLoginAtUtc { get; init; }
    public bool? CanMigrate { get; init; }
    public IReadOnlyList<RecruivaUserTokenExport> Tokens { get; init; } = [];
}

public sealed record RecruivaUserTokenExport
{
    public required string LoginProvider { get; init; }
    public required string Name { get; init; }
    public string? Value { get; init; }
}

public sealed record RecruivaCandidateExport : RecruivaSourceRow
{
    public required string Email { get; init; }
    public required string Name { get; init; }
    public string? Status { get; init; }
    public DateTime? CreatedAtUtc { get; init; }
    public DateTime? UpdatedAtUtc { get; init; }
    public bool? CanMigrate { get; init; }
}

public sealed record RecruivaAdvertiserExport : RecruivaSourceRow
{
    public required string Email { get; init; }
    public required string Name { get; init; }
    public string? Status { get; init; }
    public string? LogoUrl { get; init; }
    public DateTime? CreatedAtUtc { get; init; }
    public DateTime? UpdatedAtUtc { get; init; }
    public bool? CanMigrate { get; init; }
}

public sealed record RecruivaJobExport : RecruivaSourceRow
{
    public required string AdvertiserSourceId { get; init; }
    public required string Title { get; init; }
    public required string Status { get; init; }
    public string? Description { get; init; }
    public DateTime? ExpirationDateUtc { get; init; }
    public DateTime? PublishedAtUtc { get; init; }
    public DateTime? CreatedAtUtc { get; init; }
    public DateTime? UpdatedAtUtc { get; init; }
}

public sealed record RecruivaResumeExport : RecruivaSourceRow
{
    public required string CandidateSourceId { get; init; }
    public required string Title { get; init; }
    public string? Summary { get; init; }
    public bool? IsActive { get; init; }
    public string? SourceFileName { get; init; }
    public string? SourceFileUrl { get; init; }
    public DateTime? CreatedAtUtc { get; init; }
    public DateTime? UpdatedAtUtc { get; init; }
}

public sealed record RecruivaFileExport : RecruivaSourceRow
{
    public required string OwnerType { get; init; }
    public required string OwnerSourceId { get; init; }
    public string? SourceUrl { get; init; }
    public required string ContentHash { get; init; }
    public long SizeBytes { get; init; }
    public bool? CanMigrate { get; init; }
}

public sealed record RecruivaApplicationExport : RecruivaSourceRow
{
    public required string CandidateSourceId { get; init; }
    public required string JobSourceId { get; init; }
    public string? ResumeSourceId { get; init; }
    public required string Status { get; init; }
    public string? Notes { get; init; }
    public int? Version { get; init; }
    public DateTime? AppliedAtUtc { get; init; }
    public DateTime? ViewedAtUtc { get; init; }
    public DateTime? SelectedAtUtc { get; init; }
    public DateTime? RejectedAtUtc { get; init; }
    public DateTime? CreatedAtUtc { get; init; }
    public DateTime? UpdatedAtUtc { get; init; }
}

public sealed record RecruivaTransitionExport : RecruivaSourceRow
{
    public required string ApplicationSourceId { get; init; }
    public required string Status { get; init; }
    public required DateTime OccurredAtUtc { get; init; }
    public string? Note { get; init; }
    public string? Responsible { get; init; }
    public int? Version { get; init; }
    public DateTime? CreatedAtUtc { get; init; }
}

public sealed record RecruivaMigrationProblem(string Kind, string Entity, string SourceId, string Message);

public sealed record RecruivaMigrationDryRunReport
{
    public required string Version { get; init; }
    public required bool CanConfirm { get; init; }
    public required IReadOnlyDictionary<string, int> Counts { get; init; }
    public required IReadOnlyDictionary<string, string> Hashes { get; init; }
    public required IReadOnlyDictionary<string, Guid> IdMap { get; init; }
    public required IReadOnlyList<RecruivaMigrationProblem> Problems { get; init; }
}
