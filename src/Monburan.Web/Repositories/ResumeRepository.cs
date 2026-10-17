using Microsoft.EntityFrameworkCore;

using Monburan.Core.Entities;
using Monburan.Core.Interfaces.Repositories;
using Monburan.Core.Requests;
using Monburan.Core.ValueObjects;

namespace Monburan.Web.Repositories;

public class ResumeRepository : IResumeRepository
{
    private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;

    public ResumeRepository(IDbContextFactory<ApplicationDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<RequestResult<Resume>> CreateAsync(Resume entity)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        context.Resumes.Add(entity);
        await context.SaveChangesAsync();
        return RequestResult<Resume>.Success(entity);
    }

    public async Task<RequestResult<Resume>> DeleteAsync(Id id)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var resume = await context.Resumes.FindAsync(id);
        if (resume == null)
            return RequestResult<Resume>.EntityNotFound("Resume", id.Value, "Currículo não encontrado.");

        resume.IsDeleted = true;
        resume.DeletedAt = DateTime.UtcNow;
        
        context.Resumes.Update(resume);
        await context.SaveChangesAsync();
        return RequestResult<Resume>.Success(resume);
    }

    public async Task<RequestResult<IEnumerable<Resume>>> GetAllAsync(int page, int size)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var resumes = await context.Resumes
            .Include(r => r.Candidate)
            .Where(r => !r.IsDeleted)
            .OrderByDescending(r => r.CreatedAt)
            .Skip((page - 1) * size)
            .Take(size)
            .ToListAsync();

        return RequestResult<IEnumerable<Resume>>.Success(resumes);
    }

    public async Task<RequestResult<Resume>> GetByIdAsync(Id id)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var resume = await context.Resumes
            .Include(r => r.Candidate)
            .FirstOrDefaultAsync(r => r.Id == id && !r.IsDeleted);

        if (resume == null)
            return RequestResult<Resume>.EntityNotFound("Resume", id.Value, "Currículo não encontrado.");

        return RequestResult<Resume>.Success(resume);
    }

    public async Task<RequestResult<Resume>> UpdateAsync(Resume entity)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        context.Resumes.Update(entity);
        await context.SaveChangesAsync();
        return RequestResult<Resume>.Success(entity);
    }

    public async Task<RequestResult<IEnumerable<Resume>>> ListByCandidateAsync(Guid candidateId)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var candidateKey = Id.Create(candidateId);
        var resumes = await context.Resumes
            .AsNoTracking()
            .Where(r => !r.IsDeleted && r.CandidateId == candidateKey)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync();

        return RequestResult<IEnumerable<Resume>>.Success(resumes);
    }
}
