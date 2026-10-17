using Microsoft.EntityFrameworkCore;

using Monburan.Core.DTOs.Response;
using Monburan.Core.Entities;
using Monburan.Core.Enums;
using Monburan.Core.Interfaces.Repositories;
using Monburan.Core.Requests;
using Monburan.Core.ValueObjects;

namespace Monburan.Web.Repositories;

public class AdvertiserRepository : IAdvertiserRepository
{
    private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;

    public AdvertiserRepository(IDbContextFactory<ApplicationDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<RequestResult<Advertiser>> CreateAsync(Advertiser entity)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        context.Advertisers.Add(entity);
        await context.SaveChangesAsync();
        return RequestResult<Advertiser>.Success(entity);
    }

    public async Task<RequestResult<Advertiser>> DeleteAsync(Id id)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var advertiser = await context.Advertisers.FindAsync(id);
        if (advertiser == null)
            return RequestResult<Advertiser>.EntityNotFound("Advertiser", id.Value, "Anunciante não encontrado.");

        advertiser.IsDeleted = true;
        advertiser.DeletedAt = DateTime.UtcNow;
        
        context.Advertisers.Update(advertiser);
        await context.SaveChangesAsync();
        return RequestResult<Advertiser>.Success(advertiser);
    }

    public async Task<RequestResult<IEnumerable<Advertiser>>> GetAllAsync(int page, int size)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var advertisers = await context.Advertisers
            .Include(a => a.Address)
            .Where(a => !a.IsDeleted)
            .OrderByDescending(a => a.CreatedAt)
            .Skip((page - 1) * size)
            .Take(size)
            .ToListAsync();

        return RequestResult<IEnumerable<Advertiser>>.Success(advertisers);
    }

    public async Task<RequestResult<Advertiser>> GetByIdAsync(Id id)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var advertiser = await context.Advertisers
            .Include(a => a.Address)
            .FirstOrDefaultAsync(a => a.Id == id && !a.IsDeleted);

        if (advertiser == null)
            return RequestResult<Advertiser>.EntityNotFound("Advertiser", id.Value, "Anunciante não encontrado.");

        return RequestResult<Advertiser>.Success(advertiser);
    }

    public async Task<RequestResult<Advertiser>> UpdateAsync(Advertiser entity)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        context.Advertisers.Update(entity);
        await context.SaveChangesAsync();
        return RequestResult<Advertiser>.Success(entity);
    }

    public async Task<RequestResult<PagedResponse<Advertiser>>> SearchAsync(string? searchTerm, EAdvertiserStatus? status, int page, int size)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var query = context.Advertisers
            .AsNoTracking()
            .Include(a => a.Address)
            .Where(a => !a.IsDeleted);

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var term = searchTerm.Trim().ToLower();
            query = query.Where(a =>
                a.Name.ToLower().Contains(term) ||
                a.Email.ToLower().Contains(term) ||
                (a.TaxId != null && a.TaxId.ToLower().Contains(term)));
        }

        if (status.HasValue)
            query = query.Where(a => a.Status == status.Value);

        // Filtro antes da paginação — TotalCount é o total filtrado, não o tamanho da página
        var total = await query.CountAsync();
        var advertisers = await query
            .OrderByDescending(a => a.CreatedAt)
            .Skip((page - 1) * size)
            .Take(size)
            .ToListAsync();

        return RequestResult<PagedResponse<Advertiser>>.Success(new PagedResponse<Advertiser>
        {
            Data = advertisers,
            TotalCount = total,
            Page = page,
            Size = size
        });
    }
}
