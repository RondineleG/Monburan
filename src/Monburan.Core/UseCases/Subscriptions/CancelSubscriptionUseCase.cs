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

public sealed class CancelSubscriptionUseCase : IUseCase<CancelSubscriptionRequest, SubscriptionResponse>
{
    private readonly ISubscriptionRepository _subscriptionRepository;
    private readonly IBaseRepository<Advertiser> _advertiserRepository;

    public CancelSubscriptionUseCase(
        ISubscriptionRepository subscriptionRepository,
        IBaseRepository<Advertiser> advertiserRepository)
    {
        _subscriptionRepository = subscriptionRepository;
        _advertiserRepository = advertiserRepository;
    }

    public async Task<RequestResult<SubscriptionResponse>> ExecuteAsync(CancelSubscriptionRequest request, CancellationToken cancellationToken = default)
    {
        // Buscar assinatura
        var subscriptionResult = await _subscriptionRepository.GetByIdAsync(Id.Create(request.SubscriptionId), cancellationToken);
        if (subscriptionResult.Status != EResultStatus.Success || subscriptionResult.Data == null)
            return RequestResult<SubscriptionResponse>.EntityNotFound("Subscription", request.SubscriptionId, "Assinatura não encontrada.");

        var subscription = subscriptionResult.Data;

        // A assinatura precisa ser do anunciante autenticado: sem esta checagem, qualquer
        // anunciante cancelaria a assinatura de outro.
        if (request.AdvertiserId.HasValue && subscription.AdvertiserId?.Value != request.AdvertiserId.Value)
            return RequestResult<SubscriptionResponse>.EntityNotFound("Subscription", request.SubscriptionId, "Assinatura não encontrada.");

        // Assinatura de candidato não é cancelável por este fluxo (é de anunciante)
        if (subscription.AdvertiserId is null)
            return RequestResult<SubscriptionResponse>.EntityNotFound("Subscription", request.SubscriptionId, "Assinatura não encontrada.");

        // Verificar se está ativa
        if (subscription.Status != ESubscriptionStatus.Active)
            return RequestResult<SubscriptionResponse>.WithDomainError("A assinatura não está ativa e não pode ser cancelada.");

        var advertiserResult = await _advertiserRepository.GetByIdAsync(subscription.AdvertiserId, cancellationToken);
        if (advertiserResult.Status != EResultStatus.Success || advertiserResult.Data == null)
            return RequestResult<SubscriptionResponse>.FromResult(advertiserResult);

        var advertiser = advertiserResult.Data;

        // Cancelar assinatura e voltar para o plano Free — as duas gravações saem na mesma
        // transação, para o plano do anunciante nunca divergir do estado da assinatura.
        subscription.Status = ESubscriptionStatus.Cancelled;
        subscription.EndDate = DateTime.UtcNow;
        subscription.CancellationReason = request.Reason;
        advertiser.ActivePlan = "Free";

        var cancelResult = await _subscriptionRepository.CancelAsync(subscription, advertiser, "Free", cancellationToken);
        if (cancelResult.Status != EResultStatus.Success)
            return RequestResult<SubscriptionResponse>.FromResult(cancelResult);

        return RequestResult<SubscriptionResponse>.Success(new SubscriptionResponse
        {
            Id = subscription.Id.Value,
            PlanName = "Free",
            Status = ESubscriptionStatus.Cancelled.ToString(),
            StartDate = subscription.StartDate,
            EndDate = subscription.EndDate,
            Price = 0
        });
    }
}
