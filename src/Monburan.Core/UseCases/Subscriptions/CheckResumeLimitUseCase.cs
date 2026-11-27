using Monburan.Core.Entities;
using Monburan.Core.Enums;
using Monburan.Core.Interfaces.Repositories;
using Monburan.Core.Interfaces.Repositories.Base;
using Monburan.Core.Requests;
using Monburan.Core.ValueObjects;

namespace Monburan.Core.UseCases.Subscriptions;

/// <summary>Resultado do limite de currículos. <c>MaxResumes = -1</c> significa ilimitado.</summary>
public sealed record CheckResumeLimitResponse(bool CanCreate, int MaxResumes, int CurrentResumes, string PlanName);

/// <summary>
/// Limite de currículos do plano do candidato (<see cref="SubscriptionPlan.MaxResumes"/>): o da assinatura ativa ou, sem
/// ela, o do plano gratuito de candidato. Sem catálogo de planos (banco sem seed) não trava ninguém: vale como ilimitado.
/// </summary>
public sealed class CheckResumeLimitUseCase
{
    public const string FreePlanName = "Free";

    private readonly ISubscriptionRepository _subscriptions;
    private readonly IBaseRepository<SubscriptionPlan> _plans;
    private readonly IResumeRepository _resumes;

    public CheckResumeLimitUseCase(ISubscriptionRepository subscriptions, IBaseRepository<SubscriptionPlan> plans, IResumeRepository resumes)
    {
        _subscriptions = subscriptions;
        _plans = plans;
        _resumes = resumes;
    }

    public async Task<RequestResult<CheckResumeLimitResponse>> ExecuteAsync(Guid candidateId, CancellationToken cancellationToken = default)
    {
        var active = await _subscriptions.GetActiveByCandidateIdAsync(Id.Create(candidateId), cancellationToken);
        if (active.Status != EResultStatus.Success)
            return RequestResult<CheckResumeLimitResponse>.FromResult(active);

        var plan = active.Data?.Plan;
        if (plan is null)
        {
            var all = await _plans.GetAllAsync(1, 100, cancellationToken);
            plan = all.Status == EResultStatus.Success
                ? all.Data!.FirstOrDefault(p => p.Audience == EPlanAudience.Candidate && p.IsActive && p.Price == 0)
                : null;
        }

        var maxResumes = plan?.MaxResumes ?? -1;
        var count = await _resumes.CountByCandidateAsync(candidateId, cancellationToken);
        if (count.Status != EResultStatus.Success)
            return RequestResult<CheckResumeLimitResponse>.FromResult(count);

        var current = count.Data;
        var canCreate = maxResumes < 0 || current < maxResumes;
        return RequestResult<CheckResumeLimitResponse>.Success(
            new CheckResumeLimitResponse(canCreate, maxResumes, current, plan?.Name ?? FreePlanName));
    }
}
