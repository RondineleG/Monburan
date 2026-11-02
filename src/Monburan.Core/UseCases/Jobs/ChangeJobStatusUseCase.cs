using Monburan.Core.DTOs.Response;
using Monburan.Core.Entities;
using Monburan.Core.Enums;
using Monburan.Core.Interfaces.Repositories.Base;
using Monburan.Core.Interfaces.UseCases;
using Monburan.Core.Mappers;
using Monburan.Core.Requests;
using Monburan.Core.UseCases.Subscriptions;
using Monburan.Core.ValueObjects;

namespace Monburan.Core.UseCases.Jobs;

public sealed record ChangeJobStatusRequest(Guid JobId, Guid AdvertiserId, EJobStatus NewStatus);

/// <summary>
/// Ciclo de vida da vaga pelo anunciante: pausar (para de receber candidaturas) e
/// reativar (pausada ou expirada com validade renovada). Reativar conta no limite do plano.
/// </summary>
public sealed class ChangeJobStatusUseCase : IUseCase<ChangeJobStatusRequest, JobResponse>
{
    private readonly IBaseRepository<Job> _jobRepository;
    private readonly CheckJobLimitUseCase _checkJobLimit;

    public ChangeJobStatusUseCase(IBaseRepository<Job> jobRepository, CheckJobLimitUseCase checkJobLimit)
    {
        _jobRepository = jobRepository;
        _checkJobLimit = checkJobLimit;
    }

    public async Task<RequestResult<JobResponse>> ExecuteAsync(ChangeJobStatusRequest request, CancellationToken cancellationToken = default)
    {
        if (request.NewStatus is not (EJobStatus.Active or EJobStatus.Paused))
            return RequestResult<JobResponse>.WithValidationError("Situação de vaga inválida.", nameof(request.NewStatus));

        var jobResult = await _jobRepository.GetByIdAsync(Id.Create(request.JobId), cancellationToken);
        // Vaga de outro anunciante responde como inexistente — não revela o id
        if (jobResult.Status != EResultStatus.Success || jobResult.Data is null
            || jobResult.Data.AdvertiserId?.Value != request.AdvertiserId)
        {
            return RequestResult<JobResponse>.EntityNotFound("Job", request.JobId, "Vaga não encontrada.");
        }

        var job = jobResult.Data;
        if (job.Status == request.NewStatus)
            return RequestResult<JobResponse>.Success(JobResponseMapper.ToResponse(job));

        if (request.NewStatus == EJobStatus.Paused && job.Status != EJobStatus.Active)
            return RequestResult<JobResponse>.WithDomainError("Só é possível pausar vagas ativas.");

        if (request.NewStatus == EJobStatus.Active)
        {
            if (job.Status is not (EJobStatus.Paused or EJobStatus.Expired))
                return RequestResult<JobResponse>.WithDomainError("Esta vaga não pode ser reativada.");

            if (job.ExpirationDate.Date < DateTime.UtcNow.Date)
                return RequestResult<JobResponse>.WithDomainError("A validade da vaga já passou. Edite a data de expiração para reativá-la.");

            var limit = await _checkJobLimit.ExecuteAsync(new CheckJobLimitRequest(request.AdvertiserId), cancellationToken);
            if (limit.Status != EResultStatus.Success)
                return RequestResult<JobResponse>.FromResult(limit);

            if (!limit.Data!.CanCreate)
            {
                return RequestResult<JobResponse>.WithDomainError(
                    $"Seu plano {limit.Data.PlanName} permite até {limit.Data.MaxJobs} vagas ativas. " +
                    "Encerre uma vaga ou mude de plano para reativar esta.");
            }
        }

        job.Status = request.NewStatus;
        var updateResult = await _jobRepository.UpdateAsync(job, cancellationToken);
        if (updateResult.Status != EResultStatus.Success)
            return RequestResult<JobResponse>.FromResult(updateResult);

        return RequestResult<JobResponse>.Success(JobResponseMapper.ToResponse(job));
    }
}
