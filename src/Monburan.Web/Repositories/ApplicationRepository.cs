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

    public async Task<RequestResult<Application>> CreateAsync(Application entity, CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var existing = await FindCurrentAsync(context, entity.CandidateId, entity.JobId, cancellationToken);
        if (existing is not null)
            return DuplicateResult(existing, entity);

        try
        {
            context.Applications.Add(entity);
            await context.SaveChangesAsync(cancellationToken);
            return RequestResult<Application>.Success(entity);
        }
        catch (DbUpdateException)
        {
            context.Entry(entity).State = EntityState.Detached;
            existing = await FindCurrentAsync(context, entity.CandidateId, entity.JobId, cancellationToken);
            if (existing is not null)
                return DuplicateResult(existing, entity);

            return RequestResult<Application>.WithDomainError("Não foi possível concluir sua candidatura. Você pode já ter se candidatado a esta vaga.");
        }
    }


    private static Task<Application?> FindCurrentAsync(ApplicationDbContext context, Id candidateId, Id jobId, CancellationToken cancellationToken) =>
        context.Applications
            .AsNoTracking()
            .FirstOrDefaultAsync(application => !application.IsDeleted &&
                application.CandidateId == candidateId &&
                application.JobId == jobId,
                cancellationToken);

    private static RequestResult<Application> DuplicateResult(Application existing, Application requested) =>
        SamePayload(existing, requested)
            ? RequestResult<Application>.Success(existing)
            : RequestResult<Application>.EntityAlreadyExists(
                "Application", $"{requested.CandidateId.Value}/{requested.JobId.Value}",
                "Você já se candidatou a esta vaga com outros dados.");

    private static bool SamePayload(Application existing, Application requested) =>
        existing.HasSamePayload(requested.ResumeId, requested.Notes);

    public async Task<RequestResult<Application>> DeleteAsync(Id id, CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var application = await context.Applications.FindAsync([id], cancellationToken);
        if (application == null)
            return RequestResult<Application>.EntityNotFound("Application", id.Value, "Candidatura não encontrada.");

        application.IsDeleted = true;
        application.DeletedAt = DateTime.UtcNow;
        
        context.Applications.Update(application);
        await context.SaveChangesAsync(cancellationToken);
        return RequestResult<Application>.Success(application);
    }

    public async Task<RequestResult<IEnumerable<Application>>> GetAllAsync(int page, int size, CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var applications = await context.Applications
            .Include(a => a.Candidate)
            .Include(a => a.Job)
            .Where(a => !a.IsDeleted)
            .OrderByDescending(a => a.CreatedAt)
            .Skip((page - 1) * size)
            .Take(size)
            .ToListAsync(cancellationToken);

        return RequestResult<IEnumerable<Application>>.Success(applications);
    }

    public async Task<RequestResult<Application>> GetByIdAsync(Id id, CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var application = await context.Applications
            .Include(a => a.Candidate)
            .Include(a => a.Job)
            .FirstOrDefaultAsync(a => a.Id == id && !a.IsDeleted, cancellationToken);

        if (application == null)
            return RequestResult<Application>.EntityNotFound("Application", id.Value, "Candidatura não encontrada.");

        return RequestResult<Application>.Success(application);
    }

    public async Task<RequestResult<Application>> UpdateAsync(Application entity, CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        context.Applications.Attach(entity);
        var entry = context.Entry(entity);
        entry.Property(application => application.Version).OriginalValue = entity.Version - 1;
        entry.State = EntityState.Modified;
        try
        {
            await context.SaveChangesAsync(cancellationToken);
            return RequestResult<Application>.Success(entity);
        }
        catch (DbUpdateConcurrencyException)
        {
            return RequestResult<Application>.WithConflict("A candidatura foi atualizada por outra operação. Recarregue antes de tentar novamente.");
        }
    }

    public async Task<RequestResult<IEnumerable<Application>>> ListByCandidateAsync(Guid candidateId, int page = 1, int size = 0, CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var candidateKey = Id.Create(candidateId);
        var query = context.Applications
            .AsNoTracking()
            .Include(a => a.Job)
            .ThenInclude(job => job!.Advertiser)
            .Where(a => !a.IsDeleted && a.CandidateId == candidateKey)
            .OrderByDescending(a => a.CreatedAt);

        // Sem paginação, a listagem para no teto: materializar todas as candidaturas de um
        // candidato com dezenas de milhares de registros estourava a memória do servidor
        var (skip, take) = Pagination.For(page, size, IApplicationRepository.MaxListSize);
        var applications = await query.Skip(skip).Take(take).ToListAsync(cancellationToken);

        return RequestResult<IEnumerable<Application>>.Success(applications);
    }

    public async Task<RequestResult<IEnumerable<Application>>> ListByAdvertiserAsync(Guid advertiserId, Guid? jobId = null, int page = 1, int size = 0, CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
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

        // Sem paginação, a listagem para no teto: uma empresa com dezenas de milhares de
        // candidaturas não pode materializar tudo em memória a cada requisição
        var (skip, take) = Pagination.For(page, size, IApplicationRepository.MaxListSize);
        var applications = await query
            .OrderByDescending(a => a.CreatedAt)
            .Skip(skip).Take(take)
            .ToListAsync(cancellationToken);

        return RequestResult<IEnumerable<Application>>.Success(applications);
    }

    public async Task<RequestResult<Application?>> FindByCandidateAndJobAsync(Guid candidateId, Guid jobId, CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var application = await context.Applications
            .AsNoTracking()
            .FirstOrDefaultAsync(a => !a.IsDeleted &&
                a.CandidateId == Id.Create(candidateId) &&
                a.JobId == Id.Create(jobId),
                cancellationToken);

        return RequestResult<Application?>.Success(application);
    }

    public async Task<bool> CanAdvertiserViewResumeAsync(Guid advertiserId, Guid resumeId, Guid candidateId, CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var advertiserKey = Id.Create(advertiserId);
        var candidateKey = Id.Create(candidateId);
        // Join explícito (em vez de navegar a.Job): funciona igual no PostgreSQL e no InMemory dos testes
        return await context.Applications
            .AsNoTracking()
            .Join(
                context.Jobs.AsNoTracking().Where(job => job.AdvertiserId == advertiserKey),
                application => application.JobId,
                job => job.Id,
                (application, _) => application)
            .AnyAsync(a => !a.IsDeleted &&
                a.ResumeId == resumeId &&
                a.CandidateId == candidateKey,
                cancellationToken);
    }

    public async Task<RequestResult<IReadOnlyDictionary<EApplicationStatus, int>>> CountByStatusForCandidateAsync(Guid candidateId, CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var candidateKey = Id.Create(candidateId);
        var grouped = await context.Applications
            .AsNoTracking()
            .Where(a => !a.IsDeleted && a.CandidateId == candidateKey)
            .GroupBy(a => a.Status)
            .Select(group => new { Status = group.Key, Count = group.Count() })
            .ToListAsync(cancellationToken);

        return RequestResult<IReadOnlyDictionary<EApplicationStatus, int>>.Success(
            grouped.ToDictionary(item => item.Status, item => item.Count));
    }
}
