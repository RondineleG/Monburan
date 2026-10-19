using Monburan.Core.DTOs.Response;
using Monburan.Core.Entities;
using Monburan.Core.Enums;
using Monburan.Core.Interfaces.Repositories.Base;
using Monburan.Core.Interfaces.UseCases;
using Monburan.Core.Requests;
using Monburan.Core.Services;
using Monburan.Core.ValueObjects;

namespace Monburan.Core.UseCases.Jobs;

public class GetJobByIdUseCase : IUseCase<Guid, JobResponse>
{
    private readonly IBaseRepository<Job> _jobRepository;

    public GetJobByIdUseCase(IBaseRepository<Job> jobRepository)
    {
        _jobRepository = jobRepository;
    }

    public async Task<RequestResult<JobResponse>> ExecuteAsync(Guid request)
    {
        var result = await _jobRepository.GetByIdAsync(Id.Create(request));
        if (result.Status != EResultStatus.Success)
            return result.Status == EResultStatus.EntityNotFound
                ? RequestResult<JobResponse>.EntityNotFound("Job", request, result.Message)
                : RequestResult<JobResponse>.FromResult(result);

        var job = result.Data!;
        var response = JobResponseMapper.ToResponse(job);

        return RequestResult<JobResponse>.Success(response);
    }
}
