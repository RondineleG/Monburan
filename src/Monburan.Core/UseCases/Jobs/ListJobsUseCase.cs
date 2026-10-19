using Monburan.Core.DTOs.Request;
using Monburan.Core.DTOs.Response;
using Monburan.Core.Entities;
using Monburan.Core.Enums;
using Monburan.Core.Interfaces.Repositories;
using Monburan.Core.Interfaces.Repositories.Base;
using Monburan.Core.Interfaces.UseCases;
using Monburan.Core.Requests;
using Monburan.Core.Services;

namespace Monburan.Core.UseCases.Jobs;

public class ListJobsUseCase : IUseCase<ListJobsRequest, ListJobsResponse>
{
    private readonly IJobRepository _jobRepository;

    public ListJobsUseCase(IJobRepository jobRepository)
    {
        _jobRepository = jobRepository;
    }

    public async Task<RequestResult<ListJobsResponse>> ExecuteAsync(ListJobsRequest request)
    {
        var pagedResult = await _jobRepository.GetAllPagedAsync(request.Page, request.Size);

        var jobs = pagedResult.Data
            .Select(job => JobResponseMapper.ToResponse(job))
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
}
