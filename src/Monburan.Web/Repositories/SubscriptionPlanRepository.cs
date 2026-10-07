using Microsoft.EntityFrameworkCore;

using Monburan.Core.Entities;
using Monburan.Core.Enums;
using Monburan.Core.Interfaces.Repositories.Base;
using Monburan.Core.Requests;
using Monburan.Core.ValueObjects;

namespace Monburan.Web.Repositories;

public class SubscriptionPlanRepository : IBaseRepository<SubscriptionPlan>
{
    private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;

    public SubscriptionPlanRepository(IDbContextFactory<ApplicationDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<RequestResult<SubscriptionPlan>> CreateAsync(SubscriptionPlan entity)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        context.SubscriptionPlans.Add(entity);
        await context.SaveChangesAsync();
        return RequestResult<SubscriptionPlan>.Success(entity);
    }

    public async Task<RequestResult<SubscriptionPlan>> DeleteAsync(Id id)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var plan = await context.SubscriptionPlans.FindAsync(id);
        if (plan == null)
            return RequestResult<SubscriptionPlan>.EntityNotFound("SubscriptionPlan", id.Value, "Plano não encontrado.");

        plan.IsDeleted = true;
        plan.DeletedAt = DateTime.UtcNow;

        context.SubscriptionPlans.Update(plan);
        await context.SaveChangesAsync();
        return RequestResult<SubscriptionPlan>.Success(plan);
    }

    public async Task<RequestResult<IEnumerable<SubscriptionPlan>>> GetAllAsync(int page, int size)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var plans = await context.SubscriptionPlans
            .Where(p => !p.IsDeleted)
            .OrderByDescending(p => p.Price)
            .Skip((page - 1) * size)
            .Take(size)
            .ToListAsync();

        return RequestResult<IEnumerable<SubscriptionPlan>>.Success(plans);
    }

    public async Task<RequestResult<SubscriptionPlan>> GetByIdAsync(Id id)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var plan = await context.SubscriptionPlans
            .FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted);

        if (plan == null)
            return RequestResult<SubscriptionPlan>.EntityNotFound("SubscriptionPlan", id.Value, "Plano não encontrado.");

        return RequestResult<SubscriptionPlan>.Success(plan);
    }

    public async Task<RequestResult<SubscriptionPlan>> UpdateAsync(SubscriptionPlan entity)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        context.SubscriptionPlans.Update(entity);
        await context.SaveChangesAsync();
        return RequestResult<SubscriptionPlan>.Success(entity);
    }
}
