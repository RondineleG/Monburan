using Monburan.Core.Entities;
using Monburan.Core.Enums;
using Monburan.Core.Interfaces.Repositories.Base;
using Monburan.Core.Interfaces.UseCases;
using Monburan.Core.Requests;
using Monburan.Core.ValueObjects;

namespace Monburan.Core.UseCases.Subscriptions;

/// <summary>
/// Request para verificar limite de vagas.
/// </summary>
public record CheckJobLimitRequest(Guid AdvertiserId, int CurrentActiveJobs);

/// <summary>
/// Response para verificação de limite de vagas.
/// </summary>
public record CheckJobLimitResponse(bool CanCreate, int MaxJobs, int CurrentJobs, int RemainingSlots);

public sealed class CheckJobLimitUseCase : IUseCase<CheckJobLimitRequest, CheckJobLimitResponse>
{
    private readonly IBaseRepository<Subscription> _subscriptionRepository;
    private readonly IBaseRepository<SubscriptionPlan> _planRepository;

    public CheckJobLimitUseCase(
        IBaseRepository<Subscription> subscriptionRepository,
        IBaseRepository<SubscriptionPlan> planRepository)
    {
        _subscriptionRepository = subscriptionRepository;
        _planRepository = planRepository;
    }

    public async Task<RequestResult<CheckJobLimitResponse>> ExecuteAsync(CheckJobLimitRequest request)
    {
        // Buscar assinatura ativa do anunciante
        var allSubscriptionsResult = await _subscriptionRepository.GetAllAsync(1, 100);
        if (allSubscriptionsResult.Status != EResultStatus.Success)
            return RequestResult<CheckJobLimitResponse>.FromResult(allSubscriptionsResult);

        var activeSubscription = allSubscriptionsResult.Data!
            .FirstOrDefault(s => s.AdvertiserId == Id.Create(request.AdvertiserId)
                && s.Status == ESubscriptionStatus.Active);

        // Se não tem assinatura ativa, usar plano Free
        int maxJobs;

        if (activeSubscription != null)
        {
            var planResult = await _planRepository.GetByIdAsync(activeSubscription.PlanId);
            maxJobs = planResult.Status == EResultStatus.Success && planResult.Data != null
                ? planResult.Data.MaxJobs
                : 0;
        }
        else
        {
            // Sem assinatura = plano Free (buscar plano Free)
            var allPlansResult = await _planRepository.GetAllAsync(1, 100);
            var freePlan = allPlansResult.Status == EResultStatus.Success
                ? allPlansResult.Data!.FirstOrDefault(p => p.Name == "Free" && p.IsActive)
                : null;

            maxJobs = freePlan?.MaxJobs ?? 0;
        }

        // -1 significa ilimitado
        bool canCreate = maxJobs == -1 || request.CurrentActiveJobs < maxJobs;
        int remaining = maxJobs == -1 ? int.MaxValue : Math.Max(0, maxJobs - request.CurrentActiveJobs);

        var response = new CheckJobLimitResponse(
            CanCreate: canCreate,
            MaxJobs: maxJobs,
            CurrentJobs: request.CurrentActiveJobs,
            RemainingSlots: remaining
        );

        return RequestResult<CheckJobLimitResponse>.Success(response);
    }
}
