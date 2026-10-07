using Microsoft.EntityFrameworkCore;

using Monburan.Core.Entities;
using Monburan.Core.Interfaces.Repositories.Base;
using Monburan.Core.Requests;
using Monburan.Core.ValueObjects;

namespace Monburan.Web.Repositories;

public class CandidateRepository : IBaseRepository<Candidate>
{
    private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;

    public CandidateRepository(IDbContextFactory<ApplicationDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<RequestResult<Candidate>> CreateAsync(Candidate entity)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        context.Candidates.Add(entity);
        await context.SaveChangesAsync();
        return RequestResult<Candidate>.Success(entity);
    }

    public async Task<RequestResult<Candidate>> DeleteAsync(Id id)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var candidate = await context.Candidates.FindAsync(id);
        if (candidate == null)
            return RequestResult<Candidate>.EntityNotFound("Candidate", id.Value, "Candidato não encontrado.");

        candidate.IsDeleted = true;
        candidate.DeletedAt = DateTime.UtcNow;
        
        context.Candidates.Update(candidate);
        await context.SaveChangesAsync();
        return RequestResult<Candidate>.Success(candidate);
    }

    public async Task<RequestResult<IEnumerable<Candidate>>> GetAllAsync(int page, int size)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var candidates = await context.Candidates
            .Include(c => c.Address)
            .Where(c => !c.IsDeleted)
            .OrderByDescending(c => c.CreatedAt)
            .Skip((page - 1) * size)
            .Take(size)
            .ToListAsync();

        return RequestResult<IEnumerable<Candidate>>.Success(candidates);
    }

    public async Task<RequestResult<Candidate>> GetByIdAsync(Id id)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var candidate = await context.Candidates
            .Include(c => c.Address)
            .FirstOrDefaultAsync(c => c.Id == id && !c.IsDeleted);

        if (candidate == null)
            return RequestResult<Candidate>.EntityNotFound("Candidate", id.Value, "Candidato não encontrado.");

        return RequestResult<Candidate>.Success(candidate);
    }

    public async Task<RequestResult<Candidate>> UpdateAsync(Candidate entity)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        context.Candidates.Update(entity);
        await context.SaveChangesAsync();
        return RequestResult<Candidate>.Success(entity);
    }
}
