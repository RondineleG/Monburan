using Monburan.Core.DTOs.Response;
using Monburan.Core.Entities;
using Monburan.Core.Enums;
using Monburan.Core.Interfaces.Repositories;
using Monburan.Core.Interfaces.Repositories.Base;
using Monburan.Core.Interfaces.UseCases;
using Monburan.Core.Requests;
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
            currentSubscription = new SubscriptionResponse
            {
                Id = activeSubscription.Id.Value,
                PlanName = activeSubscription.Plan?.Name ?? "Desconhecido",
                Status = activeSubscription.Status.ToString(),
                StartDate = activeSubscription.StartDate,
                EndDate = activeSubscription.EndDate,
                Price = activeSubscription.Plan?.Price ?? 0
            };
        }

        var response = new AvailablePlansResponse
        {
            Plans = plans,
            CurrentSubscription = currentSubscription
        };

        return RequestResult<AvailablePlansResponse>.Success(response);
    }

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
        HasAiAssistant = p.HasAiAssistant
    };
}
