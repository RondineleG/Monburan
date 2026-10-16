using Microsoft.EntityFrameworkCore;

using Monburan.Core.DTOs.Response;
using Monburan.Core.Entities;
using Monburan.Core.Interfaces.Repositories;
using Monburan.Core.Interfaces.Repositories.Base;
using Monburan.Core.Requests;
using Monburan.Core.ValueObjects;

namespace Monburan.Web.Repositories;

public class JobRepository : IJobRepository
{
    private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;

    public JobRepository(IDbContextFactory<ApplicationDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<RequestResult<Job>> CreateAsync(Job entity)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        context.Jobs.Add(entity);
        await context.SaveChangesAsync();
        return RequestResult<Job>.Success(entity);
    }

    public async Task<RequestResult<Job>> DeleteAsync(Id id)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var job = await context.Jobs.FindAsync(id);
        if (job == null)
            return RequestResult<Job>.EntityNotFound("Job", id.Value, "Vaga não encontrada.");

        job.IsDeleted = true;
        job.DeletedAt = DateTime.UtcNow;
        
        context.Jobs.Update(job);
        await context.SaveChangesAsync();
        return RequestResult<Job>.Success(job);
    }

    public async Task<RequestResult<IEnumerable<Job>>> GetAllAsync(int page, int size)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var jobs = await context.Jobs
            .Include(j => j.Advertiser)
            .Where(j => !j.IsDeleted && j.Status == EJobStatus.Active)
            .OrderByDescending(j => j.CreatedAt)
            .Skip((page - 1) * size)
            .Take(size)
            .ToListAsync();

        return RequestResult<IEnumerable<Job>>.Success(jobs);
    }

    public async Task<PagedResponse<Job>> GetAllPagedAsync(int page, int size)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var query = context.Jobs
            .Include(j => j.Advertiser)
            .Where(j => !j.IsDeleted && j.Status == EJobStatus.Active);

        var totalCount = await query.CountAsync();
        var jobs = await query
            .OrderByDescending(j => j.CreatedAt)
            .Skip((page - 1) * size)
            .Take(size)
            .ToListAsync();

        return new PagedResponse<Job>
        {
            Data = jobs,
            TotalCount = totalCount,
            Page = page,
            Size = size
        };
    }

    public async Task<RequestResult<Job>> GetByIdAsync(Id id)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var job = await context.Jobs
            .Include(j => j.Advertiser)
            .FirstOrDefaultAsync(j => j.Id == id && !j.IsDeleted);

        if (job == null)
            return RequestResult<Job>.EntityNotFound("Job", id.Value, "Vaga não encontrada.");

        return RequestResult<Job>.Success(job);
    }

    public async Task<RequestResult<Job>> UpdateAsync(Job entity)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        context.Jobs.Update(entity);
        await context.SaveChangesAsync();
        return RequestResult<Job>.Success(entity);
    }

    public async Task<RequestResult<PagedResponse<Job>>> SearchAsync(
        string? searchTerm = null,
        string? city = null,
        string? state = null,
        bool? isRemote = null,
        decimal? salaryMin = null,
        decimal? salaryMax = null,
        string? category = null,
        Guid? advertiserId = null,
        int page = 1,
        int size = 10,
        string? type = null,
        string? sortBy = null,
        bool? sortDescending = null)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var query = context.Jobs
            .Include(j => j.Advertiser)
            .Include(j => j.Location)
            .Include(j => j.Salary)
            .Where(j => !j.IsDeleted && j.Status == EJobStatus.Active);

        // Filtro por advertiserId (para MyJobs)
        if (advertiserId.HasValue)
        {
            var advertiserKey = Id.Create(advertiserId.Value);
            query = query.Where(j => j.AdvertiserId == advertiserKey);
        }

        // Filtro por busca textual (aplicado no WHERE do SQL).
        // Usa ToLower().Contains() em vez de EF.Functions.ILike para manter a consulta
        // executável no provider InMemory dos testes de API — funciona igual no Npgsql
        // (embora sem pg_trgm/citext). Para buscas maiores, considerar Testcontainers.
        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var term = searchTerm.ToLower();
            query = query.Where(j =>
                j.Title.ToLower().Contains(term) ||
                j.Description.ToLower().Contains(term) ||
                (j.Category != null && j.Category.ToLower().Contains(term)));
        }

        // Filtro por cidade
        if (!string.IsNullOrWhiteSpace(city))
        {
            var cityLower = city.ToLower();
            query = query.Where(j => j.Location != null && j.Location.City != null && j.Location.City.ToLower().Contains(cityLower));
        }

        // Filtro por estado
        if (!string.IsNullOrWhiteSpace(state))
        {
            var stateUpper = state.ToUpper();
            query = query.Where(j => j.Location != null && j.Location.State == stateUpper);
        }

        // Filtro por remoto
        if (isRemote.HasValue)
        {
            query = query.Where(j => j.Location != null && j.Location.IsRemote == isRemote.Value);
        }

        // Filtro por tipo de trabalho (OnSite, Remote, Hybrid)
        if (!string.IsNullOrWhiteSpace(type))
        {
            query = query.Where(j => j.Location != null && j.Location.Type == type);
        }

        // Filtro por salário mínimo
        if (salaryMin.HasValue)
        {
            query = query.Where(j => j.Salary != null && j.Salary.Max.HasValue && j.Salary.Max >= salaryMin.Value);
        }

        // Filtro por salário máximo
        if (salaryMax.HasValue)
        {
            query = query.Where(j => j.Salary != null && j.Salary.Min.HasValue && j.Salary.Min <= salaryMax.Value);
        }

        // Filtro por categoria
        if (!string.IsNullOrWhiteSpace(category))
        {
            query = query.Where(j => j.Category == category);
        }

        // Ordenação — sortBy: "createdAt" (default), "title", "salary"
        var descending = sortDescending ?? true;
        query = sortBy?.ToLowerInvariant() switch
        {
            "title" => descending ? query.OrderByDescending(j => j.Title) : query.OrderBy(j => j.Title),
            "salary" => descending
                ? query.OrderByDescending(j => j.Salary == null ? null : j.Salary.Max)
                : query.OrderBy(j => j.Salary == null ? null : j.Salary.Max),
            _ => descending ? query.OrderByDescending(j => j.CreatedAt) : query.OrderBy(j => j.CreatedAt),
        };

        // Paginação com OFFSET/FETCH — TotalCount é o total filtrado, não o tamanho da página
        var totalJobs = await query.CountAsync();
        var jobs = await query
            .Skip((page - 1) * size)
            .Take(size)
            .ToListAsync();

        return RequestResult<PagedResponse<Job>>.Success(new PagedResponse<Job>
        {
            Data = jobs,
            TotalCount = totalJobs,
            Page = page,
            Size = size
        });
    }
}
