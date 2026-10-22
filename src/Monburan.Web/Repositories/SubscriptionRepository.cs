using Microsoft.EntityFrameworkCore;

using Monburan.Core.Entities;
using Monburan.Core.Entities.Base;
using Monburan.Core.Enums;
using Monburan.Core.Interfaces;
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

    public async Task<RequestResult<Subscription>> CreateAsync(Subscription entity, CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        context.Subscriptions.Add(entity);
        await context.SaveChangesAsync(cancellationToken);
        return RequestResult<Subscription>.Success(entity);
    }

    public async Task<RequestResult<Subscription>> DeleteAsync(Id id, CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var subscription = await context.Subscriptions.FindAsync([id], cancellationToken);
        if (subscription == null)
            return RequestResult<Subscription>.EntityNotFound("Subscription", id.Value, "Assinatura não encontrada.");

        subscription.IsDeleted = true;
        subscription.DeletedAt = DateTime.UtcNow;

        context.Subscriptions.Update(subscription);
        await context.SaveChangesAsync(cancellationToken);
        return RequestResult<Subscription>.Success(subscription);
    }

    public async Task<RequestResult<IEnumerable<Subscription>>> GetAllAsync(int page, int size, CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var subscriptions = await context.Subscriptions
            .Include(s => s.Plan)
            .Where(s => !s.IsDeleted)
            .OrderByDescending(s => s.StartDate)
            .Skip((page - 1) * size)
            .Take(size)
            .ToListAsync(cancellationToken);

        return RequestResult<IEnumerable<Subscription>>.Success(subscriptions);
    }

    public async Task<RequestResult<Subscription>> GetByIdAsync(Id id, CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var subscription = await context.Subscriptions
            .Include(s => s.Plan)
            .FirstOrDefaultAsync(s => s.Id == id && !s.IsDeleted, cancellationToken);

        if (subscription == null)
            return RequestResult<Subscription>.EntityNotFound("Subscription", id.Value, "Assinatura não encontrada.");

        return RequestResult<Subscription>.Success(subscription);
    }

    public async Task<RequestResult<Subscription>> UpdateAsync(Subscription entity, CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        context.Subscriptions.Update(entity);
        await context.SaveChangesAsync(cancellationToken);
        return RequestResult<Subscription>.Success(entity);
    }

    /// <summary>
    /// Busca todas as assinaturas de um anunciante.
    /// </summary>
    public async Task<RequestResult<IEnumerable<Subscription>>> GetByAdvertiserIdAsync(Id advertiserId, CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var subscriptions = await context.Subscriptions
            .Include(s => s.Plan)
            .Where(s => s.AdvertiserId == advertiserId && !s.IsDeleted)
            .OrderByDescending(s => s.StartDate)
            .ToListAsync(cancellationToken);

        return RequestResult<IEnumerable<Subscription>>.Success(subscriptions);
    }

    /// <summary>
    /// Busca a assinatura ativa de um anunciante.
    /// </summary>
    public async Task<RequestResult<Subscription?>> GetActiveByAdvertiserIdAsync(Id advertiserId, CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var now = DateTime.UtcNow;
        var subscription = await context.Subscriptions
            .Include(s => s.Plan)
            .FirstOrDefaultAsync(s => s.AdvertiserId == advertiserId
                && s.Status == ESubscriptionStatus.Active
                && (s.EndDate == null || s.EndDate > now)
                && !s.IsDeleted,
                cancellationToken);

        return RequestResult<Subscription?>.Success(subscription);
    }

    /// <summary>
    /// Busca a assinatura ativa de um candidato.
    /// </summary>
    public async Task<RequestResult<Subscription?>> GetActiveByCandidateIdAsync(Id candidateId, CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var now = DateTime.UtcNow;
        var subscription = await context.Subscriptions
            .Include(s => s.Plan)
            .FirstOrDefaultAsync(s => s.CandidateId == candidateId
                && s.Status == ESubscriptionStatus.Active
                && (s.EndDate == null || s.EndDate > now)
                && !s.IsDeleted,
                cancellationToken);

        return RequestResult<Subscription?>.Success(subscription);
    }

    /// <summary>
    /// Cancela a assinatura e devolve o anunciante ao plano inicial, na mesma transação.
    /// As entidades são recarregadas no mesmo contexto para gravar só as colunas que mudaram
    /// (nada de reescrever a linha inteira) e o commit é único.
    /// </summary>
    public async Task<RequestResult<IHasActivePlan>> CancelAsync(
        Subscription subscription,
        IHasActivePlan owner,
        string fallbackPlan,
        CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = context.Database.IsRelational()
            ? await context.Database.BeginTransactionAsync(cancellationToken)
            : null;

        var trackedSubscription = await context.Subscriptions
            .FirstOrDefaultAsync(s => s.Id == subscription.Id, cancellationToken);
        if (trackedSubscription is null)
            return RequestResult<IHasActivePlan>.EntityNotFound("Subscription", subscription.Id.Value, "Assinatura não encontrada.");

        trackedSubscription.Status = subscription.Status;
        trackedSubscription.EndDate = subscription.EndDate;
        trackedSubscription.CancellationReason = subscription.CancellationReason;

        var trackedOwner = await FindOwnerAsync(context, owner, cancellationToken);
        if (trackedOwner is null)
            return RequestResult<IHasActivePlan>.EntityNotFound(owner.GetType().Name, GetOwnerKey(owner), "Perfil não encontrado.");

        trackedOwner.ActivePlan = fallbackPlan;

        await context.SaveChangesAsync(cancellationToken);

        if (transaction is not null)
            await transaction.CommitAsync(cancellationToken);

        return RequestResult<IHasActivePlan>.Success(trackedOwner);
    }

    /// <summary>
    /// Troca o plano do anunciante (cancela a assinatura anterior, grava a nova e atualiza o
    /// plano ativo) numa transação só. Grava apenas as colunas alteradas de cada registro.
    /// </summary>
    public async Task<RequestResult<Subscription>> ChangePlanAsync(
        Subscription? previousSubscription,
        Subscription newSubscription,
        IHasActivePlan owner,
        CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = context.Database.IsRelational()
            ? await context.Database.BeginTransactionAsync(cancellationToken)
            : null;

        if (previousSubscription is not null)
        {
            var trackedPrevious = await context.Subscriptions
                .FirstOrDefaultAsync(s => s.Id == previousSubscription.Id, cancellationToken);
            // Sem a anterior não dá para cancelá-la: seguir criaria duas assinaturas ativas. A
            // transação não é confirmada, então nada é gravado.
            if (trackedPrevious is null)
                return RequestResult<Subscription>.EntityNotFound("Subscription", previousSubscription.Id.Value, "Assinatura anterior não encontrada.");

            trackedPrevious.Status = previousSubscription.Status;
            trackedPrevious.EndDate = previousSubscription.EndDate;
            trackedPrevious.CancellationReason = previousSubscription.CancellationReason;
        }

        context.Subscriptions.Add(newSubscription);

        var trackedOwner = await FindOwnerAsync(context, owner, cancellationToken);
        if (trackedOwner is null)
            return RequestResult<Subscription>.EntityNotFound(owner.GetType().Name, GetOwnerKey(owner), "Perfil não encontrado.");

        trackedOwner.ActivePlan = owner.ActivePlan;

        await context.SaveChangesAsync(cancellationToken);

        if (transaction is not null)
            await transaction.CommitAsync(cancellationToken);

        return RequestResult<Subscription>.Success(newSubscription);
    }

    /// <summary>Recarrega o perfil dono (empresa ou candidato) no contexto da transação.</summary>
    private static async Task<IHasActivePlan?> FindOwnerAsync(
        ApplicationDbContext context, IHasActivePlan owner, CancellationToken cancellationToken) =>
        owner switch
        {
            Advertiser advertiser => await context.Advertisers
                .FirstOrDefaultAsync(a => a.Id == advertiser.Id, cancellationToken),
            Candidate candidate => await context.Candidates
                .FirstOrDefaultAsync(c => c.Id == candidate.Id, cancellationToken),
            _ => null
        };

    private static Guid GetOwnerKey(IHasActivePlan owner) =>
        owner is BaseEntity entity ? entity.Id.Value : Guid.Empty;
}
