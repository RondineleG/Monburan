using Monburan.Core.DTOs.Request;
using Monburan.Core.DTOs.Response;
using Monburan.Core.Entities;
using Monburan.Core.Enums;
using Monburan.Core.Interfaces.Repositories;
using Monburan.Core.Interfaces.UseCases;
using Monburan.Core.Mappers;
using Monburan.Core.Requests;

namespace Monburan.Core.UseCases.Jobs;

public class ListJobsUseCase : IUseCase<ListJobsRequest, ListJobsResponse>
{
    private readonly IJobRepository _jobRepository;
    private readonly ISavedJobRepository _savedJobs;

    public ListJobsUseCase(IJobRepository jobRepository, ISavedJobRepository savedJobs)
    {
        _jobRepository = jobRepository;
        _savedJobs = savedJobs;
    }

    public async Task<RequestResult<ListJobsResponse>> ExecuteAsync(ListJobsRequest request, CancellationToken cancellationToken = default)
    {
        var pagedResult = await _jobRepository.GetAllPagedAsync(request.Page, request.Size, cancellationToken);

        var pageJobs = pagedResult.Data.ToList();
        var savedIds = await GetSavedJobIdsAsync(request.CandidateId, pageJobs);
        var jobs = pageJobs
            .Select(job => JobResponseMapper.ToResponse(job, savedIds.Contains(job.Id.Value)))
            .ToList();

        var response = new ListJobsResponse
        {
            Jobs = jobs,
            TotalCount = pagedResult.TotalCount,
            Page = request.Page,
            Size = request.Size
        };

        return RequestResult<ListJobsResponse>.Success(response);
    }

    // Uma consulta em lote por página; falha na marcação não derruba a listagem (as vagas só perdem o coração preenchido)
    private async Task<IReadOnlyCollection<Guid>> GetSavedJobIdsAsync(Guid? candidateId, IEnumerable<Job> jobs, CancellationToken cancellationToken = default)
    {
        if (!candidateId.HasValue)
            return [];

        var jobIds = jobs.Select(job => job.Id.Value).ToList();
        if (jobIds.Count == 0)
            return [];

        var saved = await _savedJobs.ListSavedJobIdsAsync(candidateId.Value, jobIds, cancellationToken);
        return saved.Status == EResultStatus.Success && saved.Data is not null ? saved.Data : [];
    }
}
