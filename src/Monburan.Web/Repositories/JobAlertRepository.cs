using Microsoft.EntityFrameworkCore;

using Monburan.Core.Entities;
using Monburan.Core.Interfaces.Repositories;
using Monburan.Core.Requests;
using Monburan.Core.ValueObjects;

namespace Monburan.Web.Repositories;

public class JobAlertRepository : IJobAlertRepository
{
    private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;

    public JobAlertRepository(IDbContextFactory<ApplicationDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<RequestResult<JobAlert>> CreateAsync(JobAlert entity, CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        context.JobAlerts.Add(entity);
        await context.SaveChangesAsync(cancellationToken);
        return RequestResult<JobAlert>.Success(entity);
    }

    public async Task<RequestResult<JobAlert>> DeleteAsync(Id id, CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var alert = await context.JobAlerts.FindAsync([id], cancellationToken);
        if (alert == null)
            return RequestResult<JobAlert>.EntityNotFound("JobAlert", id.Value, "Alerta não encontrado.");

        alert.IsDeleted = true;
        alert.DeletedAt = DateTime.UtcNow;

        context.JobAlerts.Update(alert);
        await context.SaveChangesAsync(cancellationToken);
        return RequestResult<JobAlert>.Success(alert);
    }

    public async Task<RequestResult<JobAlert>> GetByIdAsync(Id id, CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var alert = await context.JobAlerts
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == id && !item.IsDeleted, cancellationToken);

        if (alert == null)
            return RequestResult<JobAlert>.EntityNotFound("JobAlert", id.Value, "Alerta não encontrado.");

        return RequestResult<JobAlert>.Success(alert);
    }

    public async Task<RequestResult<JobAlert>> UpdateAsync(JobAlert entity, CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        context.JobAlerts.Update(entity);
        await context.SaveChangesAsync(cancellationToken);
        return RequestResult<JobAlert>.Success(entity);
    }

    public async Task<RequestResult<IEnumerable<JobAlert>>> ListByCandidateAsync(Guid candidateId, CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var candidateKey = Id.Create(candidateId);
        var alerts = await context.JobAlerts
            .AsNoTracking()
            .Where(item => !item.IsDeleted && item.CandidateId == candidateKey)
            .OrderByDescending(item => item.CreatedAt)
            .ToListAsync(cancellationToken);

        return RequestResult<IEnumerable<JobAlert>>.Success(alerts);
    }

    public async Task<RequestResult<int>> CountByCandidateAsync(Guid candidateId, CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var candidateKey = Id.Create(candidateId);
        var count = await context.JobAlerts
            .AsNoTracking()
            .CountAsync(item => !item.IsDeleted && item.CandidateId == candidateKey, cancellationToken);

        return RequestResult<int>.Success(count);
    }

    public async Task<RequestResult<int>> CountActiveByCandidateAsync(Guid candidateId, CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var candidateKey = Id.Create(candidateId);
        var count = await context.JobAlerts
            .AsNoTracking()
            .CountAsync(item => !item.IsDeleted && item.IsActive && item.CandidateId == candidateKey, cancellationToken);

        return RequestResult<int>.Success(count);
    }

    public async Task<RequestResult<IEnumerable<JobAlert>>> ListActiveAsync(CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var alerts = await context.JobAlerts
            .AsNoTracking()
            .Where(item => !item.IsDeleted && item.IsActive)
            .OrderBy(item => item.CreatedAt)
            .ToListAsync(cancellationToken);

        return RequestResult<IEnumerable<JobAlert>>.Success(alerts);
    }
}
