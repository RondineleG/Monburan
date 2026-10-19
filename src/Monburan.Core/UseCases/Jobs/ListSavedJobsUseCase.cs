using Monburan.Core.DTOs.Response;
using Monburan.Core.Enums;
using Monburan.Core.Interfaces.Repositories;
using Monburan.Core.Interfaces.UseCases;
using Monburan.Core.Requests;

namespace Monburan.Core.UseCases.Jobs;

/// <summary>Vagas salvas do candidato, mais recentes primeiro.</summary>
public class ListSavedJobsUseCase : IUseCase<Guid, ListJobsResponse>
{
    private readonly ISavedJobRepository _savedJobs;

    public ListSavedJobsUseCase(ISavedJobRepository savedJobs)
    {
        _savedJobs = savedJobs;
    }

    public async Task<RequestResult<ListJobsResponse>> ExecuteAsync(Guid request)
    {
        var result = await _savedJobs.ListByCandidateAsync(request);
        if (result.Status != EResultStatus.Success || result.Data is null)
            return RequestResult<ListJobsResponse>.FromResult(result);

        var jobs = result.Data
            .Where(saved => saved.Job is not null)
            .Select(saved => JobResponseMapper.ToResponse(saved.Job!, isSaved: true))
            .ToList();

        return RequestResult<ListJobsResponse>.Success(new ListJobsResponse
        {
            Jobs = jobs,
            TotalCount = jobs.Count,
            Page = 1,
            Size = jobs.Count
        });
    }
}
