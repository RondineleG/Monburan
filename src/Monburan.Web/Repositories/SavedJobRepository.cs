using Microsoft.EntityFrameworkCore;

using Monburan.Core.Entities;
using Monburan.Core.Interfaces.Repositories;
using Monburan.Core.Requests;
using Monburan.Core.ValueObjects;

namespace Monburan.Web.Repositories;

public class SavedJobRepository : ISavedJobRepository
{
    private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;

    public SavedJobRepository(IDbContextFactory<ApplicationDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<RequestResult<SavedJob>> SaveAsync(SavedJob savedJob, CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

        var existing = await context.SavedJobs
            .AsNoTracking()
            .FirstOrDefaultAsync(saved => saved.CandidateId == savedJob.CandidateId && saved.JobId == savedJob.JobId, cancellationToken);
        if (existing is not null)
            return RequestResult<SavedJob>.Success(existing);

        try
        {
            context.SavedJobs.Add(savedJob);
            await context.SaveChangesAsync(cancellationToken);
            return RequestResult<SavedJob>.Success(savedJob);
        }
        catch (DbUpdateException)
        {
            // Corrida entre dois cliques: o índice único barrou a segunda gravação e a vaga já está salva
            context.Entry(savedJob).State = EntityState.Detached;
            var saved = await context.SavedJobs
                .AsNoTracking()
                .FirstOrDefaultAsync(item => item.CandidateId == savedJob.CandidateId && item.JobId == savedJob.JobId, cancellationToken);

            return saved is not null
                ? RequestResult<SavedJob>.Success(saved)
                : RequestResult<SavedJob>.WithError("Não foi possível salvar a vaga.");
        }
    }

    public async Task<RequestResult<bool>> RemoveAsync(Guid candidateId, Guid jobId, CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var candidateKey = Id.Create(candidateId);
        var jobKey = Id.Create(jobId);

        var saved = await context.SavedJobs
            .FirstOrDefaultAsync(item => item.CandidateId == candidateKey && item.JobId == jobKey, cancellationToken);
        if (saved is null)
            return RequestResult<bool>.Success(false);

        context.SavedJobs.Remove(saved);
        await context.SaveChangesAsync(cancellationToken);
        return RequestResult<bool>.Success(true);
    }

    public async Task<RequestResult<IEnumerable<SavedJob>>> ListByCandidateAsync(Guid candidateId, CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var candidateKey = Id.Create(candidateId);
        var saved = await context.SavedJobs
            .AsNoTracking()
            .Include(item => item.Job)!
                .ThenInclude(job => job!.Advertiser)
            .Where(item => item.CandidateId == candidateKey && item.Job != null && !item.Job.IsDeleted)
            .OrderByDescending(item => item.CreatedAt)
            .ToListAsync(cancellationToken);

        return RequestResult<IEnumerable<SavedJob>>.Success(saved);
    }

    public async Task<RequestResult<IReadOnlyCollection<Guid>>> ListSavedJobIdsAsync(Guid candidateId, IReadOnlyCollection<Guid> jobIds, CancellationToken cancellationToken = default)
    {
        if (jobIds.Count == 0)
            return RequestResult<IReadOnlyCollection<Guid>>.Success([]);

        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var candidateKey = Id.Create(candidateId);
        var jobKeys = jobIds.Select(Id.Create).ToList();

        var savedIds = await context.SavedJobs
            .AsNoTracking()
            .Where(item => item.CandidateId == candidateKey && jobKeys.Contains(item.JobId))
            .Select(item => item.JobId.Value)
            .ToListAsync(cancellationToken);

        return RequestResult<IReadOnlyCollection<Guid>>.Success(savedIds);
    }

    public async Task<RequestResult<int>> CountByCandidateAsync(Guid candidateId, CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var candidateKey = Id.Create(candidateId);
        var count = await context.SavedJobs
            .AsNoTracking()
            .CountAsync(item => item.CandidateId == candidateKey, cancellationToken);

        return RequestResult<int>.Success(count);
    }
}
