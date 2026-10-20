using Microsoft.EntityFrameworkCore;

using Monburan.Core.DTOs.Response;
using Monburan.Core.Entities;
using Monburan.Core.Enums;
using Monburan.Core.Interfaces.Repositories;
using Monburan.Core.Requests;
using Monburan.Core.ValueObjects;

namespace Monburan.Web.Repositories;

public class CandidateRepository : ICandidateRepository
{
    private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;

    public CandidateRepository(IDbContextFactory<ApplicationDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<RequestResult<Candidate>> CreateAsync(Candidate entity, CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        context.Candidates.Add(entity);
        await context.SaveChangesAsync(cancellationToken);
        return RequestResult<Candidate>.Success(entity);
    }

    public async Task<RequestResult<Candidate>> DeleteAsync(Id id, CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var candidate = await context.Candidates.FindAsync([id], cancellationToken);
        if (candidate == null)
            return RequestResult<Candidate>.EntityNotFound("Candidate", id.Value, "Candidato não encontrado.");

        candidate.IsDeleted = true;
        candidate.DeletedAt = DateTime.UtcNow;
        
        context.Candidates.Update(candidate);
        await context.SaveChangesAsync(cancellationToken);
        return RequestResult<Candidate>.Success(candidate);
    }

    public async Task<RequestResult<IEnumerable<Candidate>>> GetAllAsync(int page, int size, CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var candidates = await context.Candidates
            .Include(c => c.Address)
            .Where(c => !c.IsDeleted)
            .OrderByDescending(c => c.CreatedAt)
            .Skip((page - 1) * size)
            .Take(size)
            .ToListAsync(cancellationToken);

        return RequestResult<IEnumerable<Candidate>>.Success(candidates);
    }

    public async Task<RequestResult<Candidate>> GetByIdAsync(Id id, CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var candidate = await context.Candidates
            .Include(c => c.Address)
            .FirstOrDefaultAsync(c => c.Id == id && !c.IsDeleted, cancellationToken);

        if (candidate == null)
            return RequestResult<Candidate>.EntityNotFound("Candidate", id.Value, "Candidato não encontrado.");

        return RequestResult<Candidate>.Success(candidate);
    }

    public async Task<RequestResult<Candidate>> UpdateAsync(Candidate entity, CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        context.Candidates.Update(entity);
        await context.SaveChangesAsync(cancellationToken);
        return RequestResult<Candidate>.Success(entity);
    }

    public async Task<RequestResult<PagedResponse<Candidate>>> SearchAsync(string? searchTerm, EAccountStatus? status, int page, int size, CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var query = context.Candidates
            .AsNoTracking()
            .Include(c => c.Address)
            .Where(c => !c.IsDeleted);

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var term = searchTerm.Trim().ToLower();
            query = query.Where(c =>
                c.Name.ToLower().Contains(term) ||
                c.Email.ToLower().Contains(term) ||
                (c.Phone != null && c.Phone.ToLower().Contains(term)));
        }

        if (status.HasValue)
            query = query.Where(c => c.Status == status.Value);

        // Filtro antes da paginação — TotalCount é o total filtrado, não o tamanho da página
        var total = await query.CountAsync(cancellationToken);
        var candidates = await query
            .OrderByDescending(c => c.CreatedAt)
            .Skip((page - 1) * size)
            .Take(size)
            .ToListAsync(cancellationToken);

        return RequestResult<PagedResponse<Candidate>>.Success(new PagedResponse<Candidate>
        {
            Data = candidates,
            TotalCount = total,
            Page = page,
            Size = size
        });
    }
}
