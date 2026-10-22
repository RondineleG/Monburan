using Monburan.Core.DTOs.Request;
using Monburan.Core.DTOs.Response;
using Monburan.Core.Entities;
using Monburan.Core.Enums;
using Monburan.Core.Interfaces.Repositories;
using Monburan.Core.Interfaces.Repositories.Base;
using Monburan.Core.Interfaces.UseCases;
using Monburan.Core.Requests;
using Monburan.Core.ValueObjects;

namespace Monburan.Core.UseCases.Subscriptions;

public sealed class UpgradePlanUseCase : IUseCase<UpgradePlanRequest, SubscriptionResponse>
{
    private readonly IBaseRepository<Advertiser> _advertiserRepository;
    private readonly IBaseRepository<SubscriptionPlan> _planRepository;
    private readonly ISubscriptionRepository _subscriptionRepository;

    public UpgradePlanUseCase(
        IBaseRepository<Advertiser> advertiserRepository,
        IBaseRepository<SubscriptionPlan> planRepository,
        ISubscriptionRepository subscriptionRepository)
    {
        _advertiserRepository = advertiserRepository;
        _planRepository = planRepository;
        _subscriptionRepository = subscriptionRepository;
    }

    public async Task<RequestResult<SubscriptionResponse>> ExecuteAsync(UpgradePlanRequest request, CancellationToken cancellationToken = default)
    {
        // Validar se anunciante existe
        var advertiserResult = await _advertiserRepository.GetByIdAsync(Id.Create(request.AdvertiserId), cancellationToken);
        if (advertiserResult.Status != EResultStatus.Success || advertiserResult.Data == null)
            return RequestResult<SubscriptionResponse>.EntityNotFound("Advertiser", request.AdvertiserId, "Anunciante não encontrado.");

        // Validar se plano existe
        var planResult = await _planRepository.GetByIdAsync(Id.Create(request.NewPlanId), cancellationToken);
        if (planResult.Status != EResultStatus.Success || planResult.Data == null)
            return RequestResult<SubscriptionResponse>.EntityNotFound("SubscriptionPlan", request.NewPlanId, "Plano não encontrado.");

        var plan = planResult.Data!;
        if (!plan.IsActive)
            return RequestResult<SubscriptionResponse>.WithDomainError("O plano selecionado não está ativo.");

        // Plano de candidato não pode ser assinado por empresa (e vice-versa)
        if (plan.Audience != EPlanAudience.Advertiser)
            return RequestResult<SubscriptionResponse>.WithDomainError("O plano selecionado não é válido para esta conta.");

        // Cancelar assinatura anterior se existir (consulta direta — a busca antiga
        // varria só as 100 primeiras assinaturas da plataforma e podia deixar duas ativas)
        var activeResult = await _subscriptionRepository.GetActiveByAdvertiserIdAsync(Id.Create(request.AdvertiserId), cancellationToken);
        if (activeResult.Status != EResultStatus.Success)
            return RequestResult<SubscriptionResponse>.FromResult(activeResult);

        var activeSubscription = activeResult.Data;
        if (activeSubscription is not null && activeSubscription.PlanId == plan.Id)
            return RequestResult<SubscriptionResponse>.WithDomainError("Você já está neste plano.");

        // A assinatura anterior é cancelada junto com a criação da nova e a atualização do plano
        // ativo do anunciante — na mesma transação, para o anunciante nunca ficar sem assinatura
        // ativa (se a criação falhar) nem com duas assinaturas ativas (se o cancelamento falhar).
        if (activeSubscription is not null)
        {
            activeSubscription.Status = ESubscriptionStatus.Cancelled;
            activeSubscription.EndDate = DateTime.UtcNow;
            activeSubscription.CancellationReason = "Upgrade para novo plano";
        }

        // Criar nova assinatura
        var newSubscription = new Subscription
        {
            Id = Id.Create(Guid.NewGuid()),
            AdvertiserId = Id.Create(request.AdvertiserId),
            PlanId = Id.Create(request.NewPlanId),
            StartDate = DateTime.UtcNow,
            EndDate = plan.Period switch
            {
                ESubscriptionPeriod.Monthly => DateTime.UtcNow.AddMonths(1),
                ESubscriptionPeriod.Annual => DateTime.UtcNow.AddYears(1),
                _ => null
            },
            Status = ESubscriptionStatus.Active,
            PaymentId = !string.IsNullOrEmpty(request.PaymentMethod) ? $"manual_{request.PaymentMethod}" : null,
            TenantId = "default"
        };

        // Atualizar ActivePlan do Advertiser
        var advertiser = advertiserResult.Data;
        advertiser.ActivePlan = plan.Name;

        var changeResult = await _subscriptionRepository.ChangePlanAsync(activeSubscription, newSubscription, advertiser, cancellationToken);
        if (changeResult.Status != EResultStatus.Success)
            return RequestResult<SubscriptionResponse>.FromResult(changeResult);

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
