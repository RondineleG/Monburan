using Monburan.Core.DTOs.Response;
using Monburan.Core.Entities;
using Monburan.Core.Enums;
using Monburan.Core.Interfaces.Repositories;
using Monburan.Core.Interfaces.Repositories.Base;
using Monburan.Core.Interfaces.UseCases;
using Monburan.Core.Mappers;
using Monburan.Core.Requests;
using Monburan.Core.ValueObjects;

namespace Monburan.Core.UseCases.Jobs;

public class GetJobByIdUseCase : IUseCase<Guid, JobResponse>
{
    private readonly IBaseRepository<Job> _jobRepository;
    private readonly ISavedJobRepository _savedJobs;

    public GetJobByIdUseCase(IBaseRepository<Job> jobRepository, ISavedJobRepository savedJobs)
    {
        _jobRepository = jobRepository;
        _savedJobs = savedJobs;
    }

    public Task<RequestResult<JobResponse>> ExecuteAsync(Guid request, CancellationToken cancellationToken = default) =>
        ExecuteAsync(request, candidateId: null, cancellationToken);

    /// <summary>Detalhe da vaga; com o candidato autenticado, marca <c>IsSaved</c>.</summary>
    public async Task<RequestResult<JobResponse>> ExecuteAsync(
        Guid request,
        Guid? candidateId,
        CancellationToken cancellationToken = default)
    {
        var result = await _jobRepository.GetByIdAsync(Id.Create(request), cancellationToken);
        if (result.Status != EResultStatus.Success)
            return result.Status == EResultStatus.EntityNotFound
                ? RequestResult<JobResponse>.EntityNotFound("Job", request, result.Message)
                : RequestResult<JobResponse>.FromResult(result);

        var job = result.Data!;
        var isSaved = await IsSavedAsync(job.Id.Value, candidateId, cancellationToken);

        return RequestResult<JobResponse>.Success(JobResponseMapper.ToResponse(job, isSaved));
    }

    // Falha na marcação não derruba o detalhe: a vaga só perde o coração preenchido
    private async Task<bool> IsSavedAsync(Guid jobId, Guid? candidateId, CancellationToken cancellationToken)
    {
        if (!candidateId.HasValue)
            return false;

        var saved = await _savedJobs.ListSavedJobIdsAsync(candidateId.Value, [jobId], cancellationToken);
        return saved.Status == EResultStatus.Success && saved.Data?.Contains(jobId) == true;
    }
}
