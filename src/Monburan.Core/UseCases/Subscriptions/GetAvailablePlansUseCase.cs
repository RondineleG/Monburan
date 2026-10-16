using Monburan.Core.DTOs.Response;
using Monburan.Core.Entities;
using Monburan.Core.Enums;
using Monburan.Core.Interfaces.Repositories.Base;
using Monburan.Core.Interfaces.UseCases;
using Monburan.Core.Requests;
using Monburan.Core.ValueObjects;

using Microsoft.EntityFrameworkCore;

namespace Monburan.Core.UseCases.Subscriptions;

public sealed class GetAvailablePlansUseCase : IUseCase<Guid, AvailablePlansResponse>
{
    private readonly IBaseRepository<SubscriptionPlan> _planRepository;
    private readonly IBaseRepository<Subscription> _subscriptionRepository;

    public GetAvailablePlansUseCase(
        IBaseRepository<SubscriptionPlan> planRepository,
        IBaseRepository<Subscription> subscriptionRepository)
    {
        _planRepository = planRepository;
        _subscriptionRepository = subscriptionRepository;
    }

    public async Task<RequestResult<AvailablePlansResponse>> ExecuteAsync(Guid advertiserId)
    {
        // Listar todos os planos ativos
        var plansResult = await _planRepository.GetAllAsync(1, 100);
        if (plansResult.Status != EResultStatus.Success)
            return RequestResult<AvailablePlansResponse>.FromResult(plansResult);

        var plans = plansResult.Data!
            .Where(p => p.IsActive)
            .Select(MapToPlanResponse)
            .ToList();

        // Buscar assinatura atual do anunciante (ignorado para visitantes sem conta)
        SubscriptionResponse? currentSubscription = null;
        var subscriptionsResult = advertiserId == Guid.Empty
            ? null
            : await _subscriptionRepository.GetAllAsync(1, 100);
        if (subscriptionsResult?.Status == EResultStatus.Success)
        {
            var activeSubscription = subscriptionsResult.Data!
                .FirstOrDefault(s => s.AdvertiserId == Id.Create(advertiserId)
                    && s.Status == ESubscriptionStatus.Active);

            if (activeSubscription != null)
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
        MaxResumes = p.MaxResumes
    };
}
