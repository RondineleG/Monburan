using Microsoft.EntityFrameworkCore;

using Monburan.Core.Entities;
using Monburan.Core.Enums;
using Monburan.Core.Interfaces.Repositories.Base;
using Monburan.Core.Requests;
using Monburan.Core.ValueObjects;

namespace Monburan.Web.Repositories;

public class SubscriptionRepository : IBaseRepository<Subscription>
{
    private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;

    public SubscriptionRepository(IDbContextFactory<ApplicationDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<RequestResult<Subscription>> CreateAsync(Subscription entity)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        context.Subscriptions.Add(entity);
        await context.SaveChangesAsync();
        return RequestResult<Subscription>.Success(entity);
    }

    public async Task<RequestResult<Subscription>> DeleteAsync(Id id)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var subscription = await context.Subscriptions.FindAsync(id);
        if (subscription == null)
            return RequestResult<Subscription>.EntityNotFound("Subscription", id.Value, "Assinatura não encontrada.");

        subscription.IsDeleted = true;
        subscription.DeletedAt = DateTime.UtcNow;

        context.Subscriptions.Update(subscription);
        await context.SaveChangesAsync();
        return RequestResult<Subscription>.Success(subscription);
    }

    public async Task<RequestResult<IEnumerable<Subscription>>> GetAllAsync(int page, int size)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var subscriptions = await context.Subscriptions
            .Include(s => s.Plan)
            .Where(s => !s.IsDeleted)
            .OrderByDescending(s => s.StartDate)
            .Skip((page - 1) * size)
            .Take(size)
            .ToListAsync();

        return RequestResult<IEnumerable<Subscription>>.Success(subscriptions);
    }

    public async Task<RequestResult<Subscription>> GetByIdAsync(Id id)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var subscription = await context.Subscriptions
            .Include(s => s.Plan)
            .FirstOrDefaultAsync(s => s.Id == id && !s.IsDeleted);

        if (subscription == null)
            return RequestResult<Subscription>.EntityNotFound("Subscription", id.Value, "Assinatura não encontrada.");

        return RequestResult<Subscription>.Success(subscription);
    }

    public async Task<RequestResult<Subscription>> UpdateAsync(Subscription entity)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        context.Subscriptions.Update(entity);
        await context.SaveChangesAsync();
        return RequestResult<Subscription>.Success(entity);
    }

    /// <summary>
    /// Busca todas as assinaturas de um anunciante.
    /// </summary>
    public async Task<RequestResult<IEnumerable<Subscription>>> GetByAdvertiserIdAsync(Id advertiserId)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var subscriptions = await context.Subscriptions
            .Include(s => s.Plan)
            .Where(s => s.AdvertiserId == advertiserId && !s.IsDeleted)
            .OrderByDescending(s => s.StartDate)
            .ToListAsync();

        return RequestResult<IEnumerable<Subscription>>.Success(subscriptions);
    }

    /// <summary>
    /// Busca a assinatura ativa de um anunciante.
    /// </summary>
    public async Task<RequestResult<Subscription?>> GetActiveByAdvertiserIdAsync(Id advertiserId)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var subscription = await context.Subscriptions
            .Include(s => s.Plan)
            .FirstOrDefaultAsync(s => s.AdvertiserId == advertiserId
                && s.Status == ESubscriptionStatus.Active
                && !s.IsDeleted);

        return RequestResult<Subscription?>.Success(subscription);
    }
}
