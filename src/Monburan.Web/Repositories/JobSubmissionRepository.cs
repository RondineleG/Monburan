using Microsoft.EntityFrameworkCore;
using Monburan.Core.DTOs.Response;
using Monburan.Core.Entities;
using Monburan.Core.Enums;
using Monburan.Core.Interfaces.Repositories;
using Monburan.Core.Requests;
using Monburan.Core.ValueObjects;
using Monburan.Web.Data;

namespace Monburan.Web.Repositories;

public class JobSubmissionRepository : IJobSubmissionRepository
{
    private const int DuplicateLimit = 20;

    private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;

    public JobSubmissionRepository(IDbContextFactory<ApplicationDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<RequestResult<JobSubmission>> CreateAsync(JobSubmission submission, CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        context.JobSubmissions.Add(submission);
        await context.SaveChangesAsync(cancellationToken);
        return RequestResult<JobSubmission>.Success(submission);
    }

    public async Task<RequestResult<JobSubmission>> GetByIdAsync(Id id, CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var submission = await context.JobSubmissions
            .AsNoTracking()
            .Include(s => s.Evidences)
            .Include(s => s.History)
            .FirstOrDefaultAsync(s => s.Id == id && !s.IsDeleted, cancellationToken);

        return submission is null
            ? RequestResult<JobSubmission>.EntityNotFound("JobSubmission", id.Value, "Sugestão não encontrada.")
            : RequestResult<JobSubmission>.Success(submission);
    }

    /// <summary>
    /// Grava a decisão. A entidade chega desanexada (carregada em outra unidade de trabalho), então os campos são
    /// copiados para a versão rastreada e só o histórico novo é inserido.
    /// </summary>
    public async Task<RequestResult<JobSubmission>> UpdateAsync(JobSubmission submission, CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var tracked = await context.JobSubmissions
            .Include(s => s.History)
            .FirstOrDefaultAsync(s => s.Id == submission.Id, cancellationToken);

        if (tracked is null)
            return RequestResult<JobSubmission>.EntityNotFound("JobSubmission", submission.Id.Value, "Sugestão não encontrada.");

        tracked.Status = submission.Status;
        tracked.ModerationNote = submission.ModerationNote;
        tracked.ModeratedBy = submission.ModeratedBy;
        tracked.ModeratedAt = submission.ModeratedAt;
        tracked.ApprovedJobId = submission.ApprovedJobId;
        tracked.DuplicateOfJobId = submission.DuplicateOfJobId;
        tracked.DuplicateOfSubmissionId = submission.DuplicateOfSubmissionId;
        tracked.AiDraftJson = submission.AiDraftJson;
        tracked.AiDraftEvidenceHash = submission.AiDraftEvidenceHash;
        tracked.AiDraftProvider = submission.AiDraftProvider;
        tracked.AiDraftAt = submission.AiDraftAt;

        var known = tracked.History.Select(h => h.Id).ToHashSet();
        foreach (var entry in submission.History.Where(h => !known.Contains(h.Id)))
        {
            entry.SubmissionId = submission.Id;
            context.JobSubmissionHistories.Add(entry);
        }

        await context.SaveChangesAsync(cancellationToken);
        return RequestResult<JobSubmission>.Success(tracked);
    }

    public async Task<RequestResult<PagedResponse<JobSubmission>>> ListAsync(ESubmissionStatus? status, int page, int size, CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var query = context.JobSubmissions.AsNoTracking().Where(s => !s.IsDeleted);
        if (status is not null)
            query = query.Where(s => s.Status == status);

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(s => s.CreatedAt)
            .Skip((page - 1) * size)
            .Take(size)
            .Include(s => s.Evidences)
            .ToListAsync(cancellationToken);

        return RequestResult<PagedResponse<JobSubmission>>.Success(new PagedResponse<JobSubmission>
        {
            Data = items,
            TotalCount = total,
            Page = page,
            Size = size
        });
    }

    public async Task<RequestResult<IReadOnlyList<DuplicateCandidate>>> FindDuplicatesAsync(
        string title,
        string? companyName,
        string? city,
        string? sourceUrl,
        IReadOnlyCollection<string> evidenceHashes,
        Id? excludeSubmissionId,
        CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var normalizedTitle = title.Trim().ToLower();
        var normalizedCompany = companyName?.Trim().ToLower();
        var normalizedCity = city?.Trim().ToLower();
        var hashes = evidenceHashes.ToList();
        var results = new List<DuplicateCandidate>();

        var jobs = await context.Jobs.AsNoTracking()
            .Where(j => !j.IsDeleted && j.Status == EJobStatus.Active)
            .Where(j => (sourceUrl != null && j.SourceUrl == sourceUrl)
                || (j.Title.ToLower() == normalizedTitle
                    && ((normalizedCompany != null && j.CompanyName != null && j.CompanyName.ToLower() == normalizedCompany)
                        || (normalizedCity != null && j.Location != null && j.Location.City != null && j.Location.City.ToLower() == normalizedCity))))
            .Take(DuplicateLimit)
            .ToListAsync(cancellationToken);

        foreach (var job in jobs)
        {
            var reasons = new List<string>();
            if (sourceUrl != null && job.SourceUrl == sourceUrl) reasons.Add("mesma fonte");
            if (job.Title.Trim().ToLower() == normalizedTitle) reasons.Add("mesmo título");
            if (normalizedCompany != null && job.CompanyName?.Trim().ToLower() == normalizedCompany) reasons.Add("mesma empresa");
            results.Add(new DuplicateCandidate("Vaga", job.Id.Value, job.Title, string.Join(", ", reasons)));
        }

        var submissions = await context.JobSubmissions.AsNoTracking()
            .Where(s => !s.IsDeleted)
            .Where(s => (sourceUrl != null && s.SourceUrl == sourceUrl)
                || (s.Title.ToLower() == normalizedTitle && normalizedCity != null && s.City != null && s.City.ToLower() == normalizedCity)
                || s.Evidences.Any(e => hashes.Contains(e.Sha256)))
            .Where(s => excludeSubmissionId == null || s.Id != excludeSubmissionId)
            .OrderByDescending(s => s.CreatedAt)
            .Take(DuplicateLimit)
            .ToListAsync(cancellationToken);

        foreach (var other in submissions)
        {
            var reasons = new List<string>();
            if (sourceUrl != null && other.SourceUrl == sourceUrl) reasons.Add("mesma fonte");
            if (other.Title.Trim().ToLower() == normalizedTitle && normalizedCity != null && other.City?.Trim().ToLower() == normalizedCity) reasons.Add("mesmo título e cidade");
            if (other.Evidences.Any(e => hashes.Contains(e.Sha256))) reasons.Add("mesma imagem de evidência");
            results.Add(new DuplicateCandidate("Sugestão", other.Id.Value, other.Title, string.Join(", ", reasons)));
        }

        return RequestResult<IReadOnlyList<DuplicateCandidate>>.Success(results);
    }
}
