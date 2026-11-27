using Monburan.Core.DTOs.Response;
using Monburan.Core.Entities;
using Monburan.Core.Enums;
using Monburan.Core.Interfaces.Repositories;
using Monburan.Core.Interfaces.Repositories.Base;
using Monburan.Core.Interfaces.Services;
using Monburan.Core.Interfaces.UseCases;
using Monburan.Core.Mappers;
using Monburan.Core.Requests;
using Monburan.Core.UseCases.JobAlerts;
using Monburan.Core.UseCases.Subscriptions;
using Monburan.Core.ValueObjects;
using Microsoft.Extensions.Logging;

namespace Monburan.Core.UseCases.Jobs;

public sealed record ChangeJobStatusRequest(Guid JobId, Guid AdvertiserId, EJobStatus NewStatus);

/// <summary>
/// Ciclo de vida da vaga pelo anunciante: pausar, reativar (pausada ou expirada com validade renovada)
/// e encerrar. Encerrar é terminal e tira a vaga da busca sem excluir o histórico. Reativar conta no limite do plano.
/// </summary>
public sealed class ChangeJobStatusUseCase : IUseCase<ChangeJobStatusRequest, JobResponse>
{
    public const string CloseJobFeature = "encerramento-vaga";
    public const string ApiStatusFeature = "gestao-status-vaga-api";
    private readonly IJobRepository _jobRepository;
    private readonly CheckJobLimitUseCase _checkJobLimit;
    private readonly IFeatureFlags _featureFlags;
    private readonly ICurrentPrincipalAccessor _currentPrincipal;
    private readonly NotifyJobAlertsOnJobCreatedUseCase _notifyAlerts;
    private readonly ILogger<ChangeJobStatusUseCase> _logger;

    public ChangeJobStatusUseCase(
        IJobRepository jobRepository,
        CheckJobLimitUseCase checkJobLimit,
        IFeatureFlags featureFlags,
        ICurrentPrincipalAccessor currentPrincipal,
        NotifyJobAlertsOnJobCreatedUseCase notifyAlerts,
        ILogger<ChangeJobStatusUseCase> logger)
    {
        _jobRepository = jobRepository;
        _checkJobLimit = checkJobLimit;
        _featureFlags = featureFlags;
        _currentPrincipal = currentPrincipal;
        _notifyAlerts = notifyAlerts;
        _logger = logger;
    }

    public async Task<RequestResult<JobResponse>> ExecuteAsync(ChangeJobStatusRequest request, CancellationToken cancellationToken = default)
    {
        if (request.NewStatus is not (EJobStatus.Active or EJobStatus.Paused or EJobStatus.Closed))
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

        if (request.NewStatus == EJobStatus.Closed)
        {
            if (job.Status is not (EJobStatus.Active or EJobStatus.Paused or EJobStatus.Expired))
                return RequestResult<JobResponse>.WithDomainError("Esta vaga não pode ser encerrada.");

            var enabled = await _featureFlags.IsEnabledAsync(CloseJobFeature, _currentPrincipal.User, cancellationToken);
            if (!enabled)
                return RequestResult<JobResponse>.WithDomainError("O encerramento de vagas ainda não está liberado para esta conta.");
        }

        if (request.NewStatus == EJobStatus.Active)
        {
            if (job.Status is not (EJobStatus.Draft or EJobStatus.Paused or EJobStatus.Expired))
                return RequestResult<JobResponse>.WithDomainError("Esta vaga não pode ser reativada.");

            if (job.Status == EJobStatus.Draft)
            {
                var draftEnabled = await _featureFlags.IsEnabledAsync(CreateJobUseCase.DraftJobFeature, _currentPrincipal.User, cancellationToken);
                if (!draftEnabled)
                    return RequestResult<JobResponse>.WithDomainError("A publicação de rascunhos ainda não está liberada para esta conta.");
            }

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

        var previousStatus = job.Status;
        job.Status = request.NewStatus;
        var updateResult = await _jobRepository.UpdateStatusAsync(job, previousStatus, request.AdvertiserId.ToString(), cancellationToken);
        if (updateResult.Status != EResultStatus.Success)
            return RequestResult<JobResponse>.FromResult(updateResult);

        var updatedJob = updateResult.Data;
        if (updatedJob is not null && updatedJob.Status != request.NewStatus)
            return RequestResult<JobResponse>.WithConflict("A situação da vaga mudou durante a operação. Atualize a página antes de tentar novamente.");

        if (previousStatus == EJobStatus.Draft && request.NewStatus == EJobStatus.Active && updatedJob is not null)
        {
            updatedJob.PublishedAt ??= DateTime.UtcNow;
            job = updatedJob;
            try
            {
                var notifyResult = await _notifyAlerts.ExecuteAsync(new NotifyJobAlertsOnJobCreatedRequest(updatedJob), cancellationToken);
                if (notifyResult.Status != EResultStatus.Success)
                    _logger.LogWarning("Rascunho {JobId} publicado, mas os alertas não foram avaliados: {Detail}", job.Id.Value, notifyResult.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Falha ao avaliar os alertas do rascunho publicado {JobId}", job.Id.Value);
            }
        }

        return RequestResult<JobResponse>.Success(JobResponseMapper.ToResponse(updatedJob ?? job));
    }
}
