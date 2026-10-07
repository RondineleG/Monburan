using Microsoft.EntityFrameworkCore;

using Monburan.Core.Entities;
using Monburan.Core.Interfaces.Repositories.Base;
using Monburan.Core.Requests;
using Monburan.Core.ValueObjects;

namespace Monburan.Web.Repositories;

public class TenantConfigRepository : IBaseRepository<TenantConfig>
{
    private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;

    public TenantConfigRepository(IDbContextFactory<ApplicationDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<RequestResult<TenantConfig>> CreateAsync(TenantConfig entity)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        context.TenantConfigs.Add(entity);
        await context.SaveChangesAsync();
        return RequestResult<TenantConfig>.Success(entity);
    }

    public async Task<RequestResult<TenantConfig>> DeleteAsync(Id id)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var tenantConfig = await context.TenantConfigs.FindAsync(id);
        if (tenantConfig == null)
            return RequestResult<TenantConfig>.EntityNotFound("TenantConfig", id.Value, "Configuração de tenant não encontrada.");

        tenantConfig.IsDeleted = true;
        tenantConfig.DeletedAt = DateTime.UtcNow;
        
        context.TenantConfigs.Update(tenantConfig);
        await context.SaveChangesAsync();
        return RequestResult<TenantConfig>.Success(tenantConfig);
    }

    public async Task<RequestResult<IEnumerable<TenantConfig>>> GetAllAsync(int page, int size)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var tenantConfigs = await context.TenantConfigs
            .Where(t => !t.IsDeleted)
            .OrderByDescending(t => t.CreatedAt)
            .Skip((page - 1) * size)
            .Take(size)
            .ToListAsync();

        return RequestResult<IEnumerable<TenantConfig>>.Success(tenantConfigs);
    }

    public async Task<RequestResult<TenantConfig>> GetByIdAsync(Id id)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var tenantConfig = await context.TenantConfigs
            .FirstOrDefaultAsync(t => t.Id == id && !t.IsDeleted);

        if (tenantConfig == null)
            return RequestResult<TenantConfig>.EntityNotFound("TenantConfig", id.Value, "Configuração de tenant não encontrada.");

        return RequestResult<TenantConfig>.Success(tenantConfig);
    }

    public async Task<RequestResult<TenantConfig>> UpdateAsync(TenantConfig entity)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        context.TenantConfigs.Update(entity);
        await context.SaveChangesAsync();
        return RequestResult<TenantConfig>.Success(entity);
    }
}
