using Monburan.Core.Entities;
using Monburan.Core.Enums;
using Monburan.Core.Interfaces.Repositories;
using Monburan.Core.Interfaces.Repositories.Base;
using Monburan.Core.Interfaces.UseCases;
using Monburan.Core.Requests;
using Monburan.Core.ValueObjects;

namespace Monburan.Core.UseCases.Jobs;

public sealed record SaveJobRequest(Guid CandidateId, Guid JobId);

/// <summary>Salva a vaga para o candidato. Idempotente: salvar de novo responde sucesso.</summary>
public class SaveJobUseCase : IUseCase<SaveJobRequest, bool>
{
    private readonly ISavedJobRepository _savedJobs;
    private readonly IBaseRepository<Job> _jobs;

    public SaveJobUseCase(ISavedJobRepository savedJobs, IBaseRepository<Job> jobs)
    {
        _savedJobs = savedJobs;
        _jobs = jobs;
    }

    public async Task<RequestResult<bool>> ExecuteAsync(SaveJobRequest request)
    {
        var jobResult = await _jobs.GetByIdAsync(Id.Create(request.JobId));
        if (jobResult.Status != EResultStatus.Success || jobResult.Data is null)
            return RequestResult<bool>.EntityNotFound("Job", request.JobId, "Vaga não encontrada.");

        var job = jobResult.Data;
        if (job.Status != EJobStatus.Active || job.ExpirationDate < DateTime.UtcNow)
            return RequestResult<bool>.EntityNotFound("Job", request.JobId, "Vaga não encontrada.");

        var result = await _savedJobs.SaveAsync(new SavedJob
        {
            Id = Id.Create(Guid.NewGuid()),
            CandidateId = Id.Create(request.CandidateId),
            JobId = Id.Create(request.JobId),
            TenantId = job.TenantId
        });

        if (result.Status != EResultStatus.Success)
            return RequestResult<bool>.FromResult(result);

        return RequestResult<bool>.Success(true);
    }
}
