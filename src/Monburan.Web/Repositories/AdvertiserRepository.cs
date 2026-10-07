using Microsoft.EntityFrameworkCore;

using Monburan.Core.Entities;
using Monburan.Core.Interfaces.Repositories.Base;
using Monburan.Core.Requests;
using Monburan.Core.ValueObjects;

namespace Monburan.Web.Repositories;

public class AdvertiserRepository : IBaseRepository<Advertiser>
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
}
