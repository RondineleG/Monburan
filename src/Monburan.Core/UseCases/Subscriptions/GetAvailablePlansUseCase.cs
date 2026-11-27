using Monburan.Core.DTOs.Response;
using Monburan.Core.Entities;
using Monburan.Core.Enums;
using Monburan.Core.Interfaces.Repositories;
using Monburan.Core.Interfaces.Repositories.Base;
using Monburan.Core.Interfaces.UseCases;
using Monburan.Core.Requests;
using Monburan.Core.Services;
using Monburan.Core.ValueObjects;

namespace Monburan.Core.UseCases.Subscriptions;

public sealed class GetAvailablePlansUseCase : IUseCase<Guid, AvailablePlansResponse>
{
    private readonly IBaseRepository<SubscriptionPlan> _planRepository;
    private readonly ISubscriptionRepository _subscriptionRepository;

    public GetAvailablePlansUseCase(
        IBaseRepository<SubscriptionPlan> planRepository,
        ISubscriptionRepository subscriptionRepository)
    {
        _planRepository = planRepository;
        _subscriptionRepository = subscriptionRepository;
    }

    public Task<RequestResult<AvailablePlansResponse>> ExecuteAsync(Guid advertiserId, CancellationToken cancellationToken = default) =>
        LoadAsync(EPlanAudience.Advertiser, advertiserId, null, cancellationToken);

    /// <summary>Planos e assinatura atuais do candidato (audiência Candidate).</summary>
    public Task<RequestResult<AvailablePlansResponse>> ExecuteForCandidateAsync(Guid candidateId, CancellationToken cancellationToken = default) =>
        LoadAsync(EPlanAudience.Candidate, null, candidateId, cancellationToken);

    private async Task<RequestResult<AvailablePlansResponse>> LoadAsync(
        EPlanAudience audience, Guid? advertiserId, Guid? candidateId, CancellationToken cancellationToken)
    {
        // Listar todos os planos ativos
        var plansResult = await _planRepository.GetAllAsync(1, 100, cancellationToken);
        if (plansResult.Status != EResultStatus.Success)
            return RequestResult<AvailablePlansResponse>.FromResult(plansResult);

        var plans = plansResult.Data!
            .Where(p => p.IsActive && p.Audience == audience)
            .Select(MapToPlanResponse)
            .ToList();

        // Buscar assinatura atual do dono (ignorado para visitantes sem conta)
        SubscriptionResponse? currentSubscription = null;
        var subscriptionResult = advertiserId is { } advId && advId != Guid.Empty
            ? await _subscriptionRepository.GetActiveByAdvertiserIdAsync(Id.Create(advId), cancellationToken)
            : candidateId is { } candId && candId != Guid.Empty
                ? await _subscriptionRepository.GetActiveByCandidateIdAsync(Id.Create(candId), cancellationToken)
                : null;
        if (subscriptionResult is { Status: not EResultStatus.Success })
            return RequestResult<AvailablePlansResponse>.FromResult(subscriptionResult);

        if (subscriptionResult?.Data is { } activeSubscription)
        {
            currentSubscription = MapToSubscriptionResponse(activeSubscription);
        }

        // Checkout recém-iniciado ainda sem confirmação do provedor: a tela mostra
        // "pagamento em processamento" em vez de fingir que nada aconteceu.
        SubscriptionResponse? pendingSubscription = null;
        if (currentSubscription is null && (advertiserId is { } ownerAdvId && ownerAdvId != Guid.Empty || candidateId is { } ownerCandId && ownerCandId != Guid.Empty))
        {
            var pendingResult = advertiserId is { } pendingAdvId && pendingAdvId != Guid.Empty
                ? await _subscriptionRepository.GetPendingByAdvertiserIdAsync(Id.Create(pendingAdvId), cancellationToken)
                : await _subscriptionRepository.GetPendingByCandidateIdAsync(Id.Create(candidateId!.Value), cancellationToken);
            if (pendingResult.Status != EResultStatus.Success)
                return RequestResult<AvailablePlansResponse>.FromResult(pendingResult);
            if (pendingResult.Data is { } pending)
                pendingSubscription = MapToSubscriptionResponse(pending);
        }

        var response = new AvailablePlansResponse
        {
            Plans = plans,
            CurrentSubscription = currentSubscription,
            PendingSubscription = pendingSubscription
        };

        return RequestResult<AvailablePlansResponse>.Success(response);
    }

    private static SubscriptionResponse MapToSubscriptionResponse(Entities.Subscription subscription) => new()
    {
        Id = subscription.Id.Value,
        PlanName = subscription.Plan?.Name ?? "Desconhecido",
        Status = subscription.Status.ToString(),
        StartDate = subscription.StartDate,
        EndDate = subscription.EndDate,
        Price = subscription.Plan?.Price ?? 0
    };

    private static PlanResponse MapToPlanResponse(SubscriptionPlan p) => new()
    {
        Id = p.Id.Value,
        Name = p.Name,
        Description = p.Description,
        Price = p.Price,
        MaxJobs = p.MaxJobs,
        HasBoost = p.HasBoost,
        HasHighlight = p.HasHighlight,
        HasAnalytics = p.HasAnalytics,
        MaxResumes = p.MaxResumes,
        Audience = p.Audience.ToString(),
        Period = p.Period.ToString(),
        HasAiAssistant = p.HasAiAssistant,
        IsAdFree = AdvertisingRules.IsAdFreePlan(p.Price)
    };
}
