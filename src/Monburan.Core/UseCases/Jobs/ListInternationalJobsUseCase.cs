using Monburan.Core.DTOs.Response;
using Monburan.Core.Enums;
using Monburan.Core.Interfaces.Repositories;
using Monburan.Core.Interfaces.UseCases;
using Monburan.Core.Mappers;
using Monburan.Core.Requests;
using Monburan.Core.Services;
using Monburan.Core.ValueObjects;

namespace Monburan.Core.UseCases.Jobs;

public sealed record ListInternationalJobsQuery(Guid CandidateId, int Page = 1, int Size = 10);

/// <summary>
/// Lista vagas internacionais (país diferente de BR) filtradas pelo perfil de carreira
/// do candidato. Recurso de plano: exige assinatura ativa com <c>HasInternationalJobs</c>.
/// </summary>
public class ListInternationalJobsUseCase : IUseCase<ListInternationalJobsQuery, PagedResponse<JobResponse>>
{
    private readonly ICareerProfileRepository _profiles;
    private readonly ISubscriptionRepository _subscriptions;
    private readonly IJobRepository _jobs;

    public ListInternationalJobsUseCase(
        ICareerProfileRepository profiles,
        ISubscriptionRepository subscriptions,
        IJobRepository jobs)
    {
        _profiles = profiles;
        _subscriptions = subscriptions;
        _jobs = jobs;
    }

    public async Task<RequestResult<PagedResponse<JobResponse>>> ExecuteAsync(
        ListInternationalJobsQuery request, CancellationToken cancellationToken = default)
    {
        var candidateKey = Id.Create(request.CandidateId);
        var subscription = await _subscriptions.GetActiveByCandidateIdAsync(candidateKey, cancellationToken);
        if (subscription.Status != EResultStatus.Success)
            return RequestResult<PagedResponse<JobResponse>>.FromResult(subscription);

        if (subscription.Data?.Plan?.HasInternationalJobs != true)
            return RequestResult<PagedResponse<JobResponse>>.WithDomainError(
                "Vagas internacionais são um recurso do plano Premium.");

        var profileResult = await _profiles.GetByCandidateIdAsync(request.CandidateId, cancellationToken);
        if (profileResult.Status != EResultStatus.Success)
            return RequestResult<PagedResponse<JobResponse>>.FromResult(profileResult);

        var profile = profileResult.Data;
        var preferredCountries = PreferredCountryCodes.Parse(profile?.PreferredCountries);
        // Feed internacional nunca traz vagas do Brasil: remove BR da lista preferida. Se o
        // perfil só tinha BR, não há país internacional para listar.
        var countries = preferredCountries?.Where(code => code != "BR").ToList();
        if (preferredCountries is not null && countries is { Count: 0 })
        {
            return RequestResult<PagedResponse<JobResponse>>.Success(new PagedResponse<JobResponse>
            {
                Data = [],
                TotalCount = 0,
                Page = request.Page,
                Size = request.Size
            });
        }

        var page = Math.Max(request.Page, 1);
        var size = Math.Clamp(request.Size, 1, 50);

        var result = await _jobs.SearchAsync(
            category: profile?.Category,
            contractType: profile?.ContractType,
            salaryMin: profile?.MinimumSalary is > 0 ? profile.MinimumSalary : null,
            type: profile?.Mode,
            page: page,
            size: size,
            sortBy: "createdAt",
            sortDescending: true,
            international: true,
            countries: countries is { Count: > 0 } ? countries : null,
            cancellationToken: cancellationToken);

        if (result.Status != EResultStatus.Success || result.Data is null)
            return RequestResult<PagedResponse<JobResponse>>.FromResult(result);

        var paged = result.Data;
        return RequestResult<PagedResponse<JobResponse>>.Success(new PagedResponse<JobResponse>
        {
            // Salário oculto pelo anunciante nunca sai no feed (vale para os dois consumidores:
            // site autenticado e integração por token).
            Data = paged.Data.Select(job =>
            {
                var response = JobResponseMapper.ToResponse(job);
                if (!response.SalaryDisplay)
                {
                    response.SalaryMin = null;
                    response.SalaryMax = null;
                }
                return response;
            }),
            TotalCount = paged.TotalCount,
            Page = paged.Page,
            Size = paged.Size
        });
    }
}
