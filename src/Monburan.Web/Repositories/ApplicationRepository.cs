using Microsoft.EntityFrameworkCore;

using Monburan.Core.Entities;
using Monburan.Core.Interfaces.Repositories;
using Monburan.Core.Requests;
using Monburan.Core.ValueObjects;

namespace Monburan.Web.Repositories;

public class ApplicationRepository : IApplicationRepository
{
    private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;

    public ApplicationRepository(IDbContextFactory<ApplicationDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<RequestResult<Application>> CreateAsync(Application entity)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var alreadyApplied = await context.Applications
            .AsNoTracking()
            .AnyAsync(application => !application.IsDeleted &&
                application.CandidateId == entity.CandidateId &&
                application.JobId == entity.JobId);
        if (alreadyApplied)
            return RequestResult<Application>.EntityAlreadyExists(
                "Application", $"{entity.CandidateId.Value}/{entity.JobId.Value}",
                "Você já se candidatou a esta vaga.");

        try
        {
            context.Applications.Add(entity);
            await context.SaveChangesAsync();
            return RequestResult<Application>.Success(entity);
        }
        catch (DbUpdateException)
        {
            context.Entry(entity).State = EntityState.Detached;
            return RequestResult<Application>.WithDomainError("Não foi possível concluir sua candidatura. Você pode já ter se candidatado a esta vaga.");
        }
    }

    public async Task<RequestResult<Application>> DeleteAsync(Id id)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var application = await context.Applications.FindAsync(id);
        if (application == null)
            return RequestResult<Application>.EntityNotFound("Application", id.Value, "Candidatura não encontrada.");

        application.IsDeleted = true;
        application.DeletedAt = DateTime.UtcNow;
        
        context.Applications.Update(application);
        await context.SaveChangesAsync();
        return RequestResult<Application>.Success(application);
    }

    public async Task<RequestResult<IEnumerable<Application>>> GetAllAsync(int page, int size)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var applications = await context.Applications
            .Include(a => a.Candidate)
            .Include(a => a.Job)
            .Where(a => !a.IsDeleted)
            .OrderByDescending(a => a.CreatedAt)
            .Skip((page - 1) * size)
            .Take(size)
            .ToListAsync();

        return RequestResult<IEnumerable<Application>>.Success(applications);
    }

    public async Task<RequestResult<Application>> GetByIdAsync(Id id)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var application = await context.Applications
            .Include(a => a.Candidate)
            .Include(a => a.Job)
            .FirstOrDefaultAsync(a => a.Id == id && !a.IsDeleted);

        if (application == null)
            return RequestResult<Application>.EntityNotFound("Application", id.Value, "Candidatura não encontrada.");

        return RequestResult<Application>.Success(application);
    }

    public async Task<RequestResult<Application>> UpdateAsync(Application entity)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        context.Applications.Update(entity);
        await context.SaveChangesAsync();
        return RequestResult<Application>.Success(entity);
    }

    public async Task<RequestResult<IEnumerable<Application>>> ListByCandidateAsync(Guid candidateId)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var candidateKey = Id.Create(candidateId);
        var applications = await context.Applications
            .AsNoTracking()
            .Include(a => a.Job)
            .Where(a => !a.IsDeleted && a.CandidateId == candidateKey)
            .OrderByDescending(a => a.CreatedAt)
            .ToListAsync();

        return RequestResult<IEnumerable<Application>>.Success(applications);
    }

    public async Task<RequestResult<IEnumerable<Application>>> ListByAdvertiserAsync(Guid advertiserId, Guid? jobId = null)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var advertiserKey = Id.Create(advertiserId);
        var query = context.Applications
            .AsNoTracking()
            .Include(a => a.Candidate)
            .Include(a => a.Job)
            .Where(a => !a.IsDeleted && a.Job != null && a.Job.AdvertiserId == advertiserKey);

        if (jobId.HasValue)
        {
            var jobKey = Id.Create(jobId.Value);
            query = query.Where(a => a.JobId == jobKey);
        }

        var applications = await query
            .OrderByDescending(a => a.CreatedAt)
            .ToListAsync();

        return RequestResult<IEnumerable<Application>>.Success(applications);
    }

    public async Task<RequestResult<IReadOnlyDictionary<EApplicationStatus, int>>> CountByStatusForCandidateAsync(Guid candidateId)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var candidateKey = Id.Create(candidateId);
        var grouped = await context.Applications
            .AsNoTracking()
            .Where(a => !a.IsDeleted && a.CandidateId == candidateKey)
            .GroupBy(a => a.Status)
            .Select(group => new { Status = group.Key, Count = group.Count() })
            .ToListAsync();

        return RequestResult<IReadOnlyDictionary<EApplicationStatus, int>>.Success(
            grouped.ToDictionary(item => item.Status, item => item.Count));
    }
}
