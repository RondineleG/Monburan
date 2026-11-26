using Monburan.Core.DTOs.Request;
using Monburan.Core.DTOs.Response;
using Monburan.Core.Enums;
using Monburan.Core.Interfaces.Repositories;
using Monburan.Core.Interfaces.UseCases;
using Monburan.Core.Requests;
using Monburan.Core.ValueObjects;

namespace Monburan.Core.UseCases.Subscriptions;

/// <summary>
/// Status pontual de uma assinatura do próprio usuário — alimenta a tela de retorno do
/// checkout enquanto o webhook do provedor confirma o pagamento.
/// </summary>
public sealed class GetSubscriptionStatusUseCase : IUseCase<SubscriptionStatusRequest, SubscriptionStatusResponse>
{
    private readonly ISubscriptionRepository _subscriptionRepository;

    public GetSubscriptionStatusUseCase(ISubscriptionRepository subscriptionRepository)
    {
        _subscriptionRepository = subscriptionRepository;
    }

    public async Task<RequestResult<SubscriptionStatusResponse>> ExecuteAsync(SubscriptionStatusRequest request, CancellationToken cancellationToken = default)
    {
        var subscriptionResult = await _subscriptionRepository.GetByIdAsync(Id.Create(request.SubscriptionId), cancellationToken);
        if (subscriptionResult.Status != EResultStatus.Success || subscriptionResult.Data == null)
            return RequestResult<SubscriptionStatusResponse>.EntityNotFound("Subscription", request.SubscriptionId, "Assinatura não encontrada.");

        var subscription = subscriptionResult.Data;

        // O dono vem da claim, nunca do corpo: sem esta checagem qualquer conta
        // consultaria o status da assinatura de outra.
        var owns = request.AdvertiserId is { } advId && subscription.AdvertiserId == Id.Create(advId)
            || request.CandidateId is { } candId && subscription.CandidateId == Id.Create(candId);
        if (!owns)
            return RequestResult<SubscriptionStatusResponse>.EntityNotFound("Subscription", request.SubscriptionId, "Assinatura não encontrada.");

        return RequestResult<SubscriptionStatusResponse>.Success(new SubscriptionStatusResponse
        {
            Id = subscription.Id.Value,
            PlanName = subscription.Plan?.Name ?? "Desconhecido",
            Status = subscription.Status.ToString(),
            Price = subscription.Plan?.Price ?? 0,
            Period = subscription.Plan?.Period.ToString() ?? string.Empty
        });
    }
}
