using Monburan.Core.DTOs.Request;
using Monburan.Core.DTOs.Response;
using Monburan.Core.Entities;
using Monburan.Core.Enums;
using Monburan.Core.Interfaces.Repositories.Base;
using Monburan.Core.Interfaces.UseCases;
using Monburan.Core.Requests;
using Monburan.Core.ValueObjects;

namespace Monburan.Core.UseCases.Subscriptions;

public sealed class UpgradePlanUseCase : IUseCase<UpgradePlanRequest, SubscriptionResponse>
{
    private readonly IBaseRepository<Advertiser> _advertiserRepository;
    private readonly IBaseRepository<SubscriptionPlan> _planRepository;
    private readonly IBaseRepository<Subscription> _subscriptionRepository;

    public UpgradePlanUseCase(
        IBaseRepository<Advertiser> advertiserRepository,
        IBaseRepository<SubscriptionPlan> planRepository,
        IBaseRepository<Subscription> subscriptionRepository)
    {
        _advertiserRepository = advertiserRepository;
        _planRepository = planRepository;
        _subscriptionRepository = subscriptionRepository;
    }

    public async Task<RequestResult<SubscriptionResponse>> ExecuteAsync(UpgradePlanRequest request)
    {
        // Validar se anunciante existe
        var advertiserResult = await _advertiserRepository.GetByIdAsync(Id.Create(request.AdvertiserId));
        if (advertiserResult.Status != EResultStatus.Success || advertiserResult.Data == null)
            return RequestResult<SubscriptionResponse>.EntityNotFound("Advertiser", request.AdvertiserId, "Anunciante não encontrado.");

        // Validar se plano existe
        var planResult = await _planRepository.GetByIdAsync(Id.Create(request.NewPlanId));
        if (planResult.Status != EResultStatus.Success || planResult.Data == null)
            return RequestResult<SubscriptionResponse>.EntityNotFound("SubscriptionPlan", request.NewPlanId, "Plano não encontrado.");

        var plan = planResult.Data!;
        if (!plan.IsActive)
            return RequestResult<SubscriptionResponse>.WithDomainError("O plano selecionado não está ativo.");

        // Cancelar assinatura anterior se existir
        var allSubscriptionsResult = await _subscriptionRepository.GetAllAsync(1, 100);
        if (allSubscriptionsResult.Status == EResultStatus.Success)
        {
            var activeSubscription = allSubscriptionsResult.Data!
                .FirstOrDefault(s => s.AdvertiserId == Id.Create(request.AdvertiserId)
                    && s.Status == ESubscriptionStatus.Active);

            if (activeSubscription != null)
            {
                activeSubscription.Status = ESubscriptionStatus.Cancelled;
                activeSubscription.EndDate = DateTime.UtcNow;
                activeSubscription.CancellationReason = "Upgrade para novo plano";
                await _subscriptionRepository.UpdateAsync(activeSubscription);
            }
        }

        // Criar nova assinatura
        var newSubscription = new Subscription
        {
            Id = Id.Create(Guid.NewGuid()),
            AdvertiserId = Id.Create(request.AdvertiserId),
            PlanId = Id.Create(request.NewPlanId),
            StartDate = DateTime.UtcNow,
            EndDate = plan.Name == "Free" ? null : DateTime.UtcNow.AddMonths(1),
            Status = ESubscriptionStatus.Active,
            PaymentId = !string.IsNullOrEmpty(request.PaymentMethod) ? $"manual_{request.PaymentMethod}" : null,
            TenantId = "default"
        };

        var createResult = await _subscriptionRepository.CreateAsync(newSubscription);
        if (createResult.Status != EResultStatus.Success)
            return RequestResult<SubscriptionResponse>.FromResult(createResult);

        // Atualizar ActivePlan do Advertiser
        var advertiser = advertiserResult.Data;
        advertiser.ActivePlan = plan.Name;
        await _advertiserRepository.UpdateAsync(advertiser);

        var response = new SubscriptionResponse
        {
            Id = newSubscription.Id.Value,
            PlanName = plan.Name,
            Status = newSubscription.Status.ToString(),
            StartDate = newSubscription.StartDate,
            EndDate = newSubscription.EndDate,
            Price = plan.Price
        };

        return RequestResult<SubscriptionResponse>.Success(response);
    }
}
