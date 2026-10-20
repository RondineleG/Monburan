using Monburan.Core.Entities;
using Monburan.Core.Enums;
using Monburan.Core.Interfaces.Repositories;
using Monburan.Core.Interfaces.Repositories.Base;
using Monburan.Core.Interfaces.UseCases;
using Monburan.Core.Requests;
using Monburan.Core.ValueObjects;

namespace Monburan.Core.UseCases.Subscriptions;

/// <summary>
/// Request para verificar limite de vagas. Sem <paramref name="CurrentActiveJobs"/>,
/// a contagem de vagas ativas do anunciante é feita no banco.
/// </summary>
public record CheckJobLimitRequest(Guid AdvertiserId, int? CurrentActiveJobs = null);

/// <summary>
/// Response para verificação de limite de vagas. <c>MaxJobs = -1</c> significa ilimitado.
/// </summary>
public record CheckJobLimitResponse(bool CanCreate, int MaxJobs, int CurrentJobs, int RemainingSlots, string PlanName);

public sealed class CheckJobLimitUseCase : IUseCase<CheckJobLimitRequest, CheckJobLimitResponse>
{
    public const string FreePlanName = "Free";

    private readonly ISubscriptionRepository _subscriptionRepository;
    private readonly IBaseRepository<SubscriptionPlan> _planRepository;
    private readonly IJobRepository _jobRepository;

    public CheckJobLimitUseCase(
        ISubscriptionRepository subscriptionRepository,
        IBaseRepository<SubscriptionPlan> planRepository,
        IJobRepository jobRepository)
    {
        _subscriptionRepository = subscriptionRepository;
        _planRepository = planRepository;
        _jobRepository = jobRepository;
    }

    public async Task<RequestResult<CheckJobLimitResponse>> ExecuteAsync(CheckJobLimitRequest request, CancellationToken cancellationToken = default)
    {
        var activeResult = await _subscriptionRepository.GetActiveByAdvertiserIdAsync(Id.Create(request.AdvertiserId), cancellationToken);
        if (activeResult.Status != EResultStatus.Success)
            return RequestResult<CheckJobLimitResponse>.FromResult(activeResult);

        var plan = activeResult.Data?.Plan;
        if (plan is null && activeResult.Data is { } subscription)
        {
            var planResult = await _planRepository.GetByIdAsync(subscription.PlanId, cancellationToken);
            plan = planResult.Status == EResultStatus.Success ? planResult.Data : null;
        }

        // Sem assinatura ativa = plano Free
        if (plan is null)
        {
            var allPlansResult = await _planRepository.GetAllAsync(1, 100, cancellationToken);
            plan = allPlansResult.Status == EResultStatus.Success
                ? allPlansResult.Data!.FirstOrDefault(p => p.Name == FreePlanName
                    && p.Audience == EPlanAudience.Advertiser && p.IsActive)
                : null;
        }

        // Catálogo de planos ausente (ex.: banco sem seed) não pode travar a publicação
        // de vagas de todos os anunciantes — trata como ilimitado.
        var maxJobs = plan?.MaxJobs ?? -1;
        var planName = plan?.Name ?? FreePlanName;

        var currentJobs = request.CurrentActiveJobs
            ?? await _jobRepository.CountActiveByAdvertiserAsync(request.AdvertiserId, cancellationToken);

        var canCreate = maxJobs == -1 || currentJobs < maxJobs;
        var remaining = maxJobs == -1 ? int.MaxValue : Math.Max(0, maxJobs - currentJobs);

        return RequestResult<CheckJobLimitResponse>.Success(
            new CheckJobLimitResponse(canCreate, maxJobs, currentJobs, remaining, planName));
    }
}
