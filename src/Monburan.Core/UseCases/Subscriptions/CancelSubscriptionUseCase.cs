using Monburan.Core.DTOs.Request;
using Monburan.Core.DTOs.Response;
using Monburan.Core.Entities;
using Monburan.Core.Enums;
using Monburan.Core.Interfaces;
using Monburan.Core.Interfaces.Repositories;
using Monburan.Core.Interfaces.Repositories.Base;
using Monburan.Core.Interfaces.Services;
using Monburan.Core.Interfaces.UseCases;
using Monburan.Core.Requests;
using Monburan.Core.ValueObjects;

namespace Monburan.Core.UseCases.Subscriptions;

public sealed class CancelSubscriptionUseCase : IUseCase<CancelSubscriptionRequest, SubscriptionResponse>
{
    private readonly ISubscriptionRepository _subscriptionRepository;
    private readonly IBaseRepository<Advertiser> _advertiserRepository;
    private readonly IBaseRepository<Candidate> _candidateRepository;
    private readonly IRecurringPaymentGateway _paymentGateway;

    public CancelSubscriptionUseCase(
        ISubscriptionRepository subscriptionRepository,
        IBaseRepository<Advertiser> advertiserRepository,
        IBaseRepository<Candidate> candidateRepository,
        IRecurringPaymentGateway paymentGateway)
    {
        _subscriptionRepository = subscriptionRepository;
        _advertiserRepository = advertiserRepository;
        _candidateRepository = candidateRepository;
        _paymentGateway = paymentGateway;
    }

    public async Task<RequestResult<SubscriptionResponse>> ExecuteAsync(CancelSubscriptionRequest request, CancellationToken cancellationToken = default)
    {
        // Buscar assinatura
        var subscriptionResult = await _subscriptionRepository.GetByIdAsync(Id.Create(request.SubscriptionId), cancellationToken);
        if (subscriptionResult.Status != EResultStatus.Success || subscriptionResult.Data == null)
            return RequestResult<SubscriptionResponse>.EntityNotFound("Subscription", request.SubscriptionId, "Assinatura não encontrada.");

        var subscription = subscriptionResult.Data;

        // A assinatura precisa ser de quem está autenticado (anunciante ou candidato):
        // sem esta checagem, qualquer conta cancelaria a assinatura de outra.
        IHasActivePlan? owner;
        if (request.AdvertiserId.HasValue)
        {
            if (subscription.AdvertiserId?.Value != request.AdvertiserId.Value)
                return RequestResult<SubscriptionResponse>.EntityNotFound("Subscription", request.SubscriptionId, "Assinatura não encontrada.");

            var advertiserResult = await _advertiserRepository.GetByIdAsync(subscription.AdvertiserId, cancellationToken);
            if (advertiserResult.Status != EResultStatus.Success || advertiserResult.Data == null)
                return RequestResult<SubscriptionResponse>.FromResult(advertiserResult);
            owner = advertiserResult.Data;
        }
        else if (request.CandidateId.HasValue)
        {
            if (subscription.CandidateId?.Value != request.CandidateId.Value)
                return RequestResult<SubscriptionResponse>.EntityNotFound("Subscription", request.SubscriptionId, "Assinatura não encontrada.");

            var candidateResult = await _candidateRepository.GetByIdAsync(subscription.CandidateId, cancellationToken);
            if (candidateResult.Status != EResultStatus.Success || candidateResult.Data == null)
                return RequestResult<SubscriptionResponse>.FromResult(candidateResult);
            owner = candidateResult.Data;
        }
        else
        {
            return RequestResult<SubscriptionResponse>.EntityNotFound("Subscription", request.SubscriptionId, "Assinatura não encontrada.");
        }

        // Verificar se está ativa
        if (subscription.Status != ESubscriptionStatus.Active)
            return RequestResult<SubscriptionResponse>.WithDomainError("A assinatura não está ativa e não pode ser cancelada.");

        if (!await _paymentGateway.CancelAsync(subscription.PaymentId, cancellationToken))
            return RequestResult<SubscriptionResponse>.WithDomainError(
                "Não foi possível cancelar a cobrança recorrente. Tente novamente ou fale com o suporte.");

        // Cancelar assinatura e voltar para o plano Free — as duas gravações saem na mesma
        // transação, para o plano do dono nunca divergir do estado da assinatura.
        subscription.Status = ESubscriptionStatus.Cancelled;
        subscription.EndDate = DateTime.UtcNow;
        subscription.CancellationReason = request.Reason;
        owner.ActivePlan = "Free";

        var cancelResult = await _subscriptionRepository.CancelAsync(subscription, owner, "Free", cancellationToken);
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
