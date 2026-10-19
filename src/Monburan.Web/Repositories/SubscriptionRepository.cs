using Microsoft.EntityFrameworkCore;

using Monburan.Core.Entities;
using Monburan.Core.Enums;
using Monburan.Core.Interfaces.Repositories;
using Monburan.Core.Requests;
using Monburan.Core.ValueObjects;

namespace Monburan.Web.Repositories;

public class SubscriptionRepository : ISubscriptionRepository
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

    /// <summary>
    /// Cancela a assinatura e devolve o anunciante ao plano inicial, na mesma transação.
    /// As entidades são recarregadas no mesmo contexto para gravar só as colunas que mudaram
    /// (nada de reescrever a linha inteira) e o commit é único.
    /// </summary>
    public async Task<RequestResult<Advertiser>> CancelAsync(
        Subscription subscription,
        Advertiser advertiser,
        string fallbackPlan)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        await using var transaction = context.Database.IsRelational()
            ? await context.Database.BeginTransactionAsync()
            : null;

        var trackedSubscription = await context.Subscriptions
            .FirstOrDefaultAsync(s => s.Id == subscription.Id);
        if (trackedSubscription is null)
            return RequestResult<Advertiser>.EntityNotFound("Subscription", subscription.Id.Value, "Assinatura não encontrada.");

        trackedSubscription.Status = subscription.Status;
        trackedSubscription.EndDate = subscription.EndDate;
        trackedSubscription.CancellationReason = subscription.CancellationReason;

        var trackedAdvertiser = await context.Advertisers
            .FirstOrDefaultAsync(a => a.Id == advertiser.Id);
        if (trackedAdvertiser is null)
            return RequestResult<Advertiser>.EntityNotFound("Advertiser", advertiser.Id.Value, "Anunciante não encontrado.");

        trackedAdvertiser.ActivePlan = fallbackPlan;

        await context.SaveChangesAsync();

        if (transaction is not null)
            await transaction.CommitAsync();

        return RequestResult<Advertiser>.Success(trackedAdvertiser);
    }

    /// <summary>
    /// Troca o plano do anunciante (cancela a assinatura anterior, grava a nova e atualiza o
    /// plano ativo) numa transação só. Grava apenas as colunas alteradas de cada registro.
    /// </summary>
    public async Task<RequestResult<Subscription>> ChangePlanAsync(
        Subscription? previousSubscription,
        Subscription newSubscription,
        Advertiser advertiser)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        await using var transaction = context.Database.IsRelational()
            ? await context.Database.BeginTransactionAsync()
            : null;

        if (previousSubscription is not null)
        {
            var trackedPrevious = await context.Subscriptions
                .FirstOrDefaultAsync(s => s.Id == previousSubscription.Id);
            if (trackedPrevious is not null)
            {
                trackedPrevious.Status = previousSubscription.Status;
                trackedPrevious.EndDate = previousSubscription.EndDate;
                trackedPrevious.CancellationReason = previousSubscription.CancellationReason;
            }
        }

        context.Subscriptions.Add(newSubscription);

        var trackedAdvertiser = await context.Advertisers
            .FirstOrDefaultAsync(a => a.Id == advertiser.Id);
        if (trackedAdvertiser is null)
            return RequestResult<Subscription>.EntityNotFound("Advertiser", advertiser.Id.Value, "Anunciante não encontrado.");

        trackedAdvertiser.ActivePlan = advertiser.ActivePlan;

        await context.SaveChangesAsync();

        if (transaction is not null)
            await transaction.CommitAsync();

        return RequestResult<Subscription>.Success(newSubscription);
    }
}
