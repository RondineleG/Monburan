using Microsoft.Extensions.Logging;

using Monburan.Core.DTOs.Request;
using Monburan.Core.DTOs.Response;
using Monburan.Core.Entities;
using Monburan.Core.Enums;
using Monburan.Core.Interfaces.Repositories;
using Monburan.Core.Interfaces.Services;
using Monburan.Core.Interfaces.UseCases;
using Monburan.Core.Requests;
using Monburan.Core.Services;
using Monburan.Core.UseCases.Notifications;
using Monburan.Core.ValueObjects;

namespace Monburan.Core.UseCases.Applications;

public class UpdateApplicationStatusUseCase : IUseCase<UpdateApplicationStatusRequest, ApplicationResponse>
{
    private readonly IApplicationRepository _applicationRepository;
    private readonly NotifyOnApplicationStatusChangedUseCase _notifyOnStatusChangedUseCase;
    private readonly ICurrentPrincipalAccessor _currentPrincipal;
    private readonly ILogger<UpdateApplicationStatusUseCase> _logger;

    public UpdateApplicationStatusUseCase(
        IApplicationRepository applicationRepository,
        NotifyOnApplicationStatusChangedUseCase notifyOnStatusChangedUseCase,
        ICurrentPrincipalAccessor currentPrincipal,
        ILogger<UpdateApplicationStatusUseCase> logger)
    {
        _applicationRepository = applicationRepository;
        _notifyOnStatusChangedUseCase = notifyOnStatusChangedUseCase;
        _currentPrincipal = currentPrincipal;
        _logger = logger;
    }

    public async Task<RequestResult<ApplicationResponse>> ExecuteAsync(UpdateApplicationStatusRequest request, CancellationToken cancellationToken = default)
    {
        var result = await _applicationRepository.GetByIdAsync(Id.Create(request.ApplicationId), cancellationToken);
        if (result.Status != EResultStatus.Success)
            return RequestResult<ApplicationResponse>.EntityNotFound("Application", request.ApplicationId, "Candidatura não encontrada.");

        var application = result.Data!;

        // Vaga externa (sem anunciante) não tem dono que possa mudar o status
        if (application.Job?.AdvertiserId is null || application.Job.AdvertiserId.Value != request.AdvertiserId)
            return RequestResult<ApplicationResponse>.EntityNotFound("Application", request.ApplicationId, "Candidatura não encontrada.");

        var oldStatus = application.Status;
        if (oldStatus == request.NewStatus)
            return RequestResult<ApplicationResponse>.Success(MapToResponse(application));

        if (!IsAllowedTransition(oldStatus, request.NewStatus))
        {
            return RequestResult<ApplicationResponse>.WithConflict(
                $"Transição de candidatura inválida: {oldStatus} → {request.NewStatus}.");
        }

        application.Status = request.NewStatus;
        application.Version++;

        switch (request.NewStatus)
        {
            case EApplicationStatus.Viewed:
                application.ViewedAt = DateTime.UtcNow;
                break;
            case EApplicationStatus.Selected:
                application.SelectedAt = DateTime.UtcNow;
                break;
            case EApplicationStatus.Rejected:
                application.RejectedAt = DateTime.UtcNow;
                break;
        }

        var historyEntry = new ApplicationStatusHistory
        {
            Id = Id.Create(Guid.NewGuid()),
            ApplicationId = application.Id,
            Status = application.Status,
            Date = DateTime.UtcNow,
            Note = request.Notes,
            Responsible = request.Responsible,
            ActorUserId = AuthenticatedUserId.From(_currentPrincipal.User),
            Version = application.Version,
            TenantId = application.TenantId
        };

        var updateResult = await _applicationRepository.UpdateStatusWithHistoryAsync(application, historyEntry, cancellationToken);
        if (updateResult.Status != EResultStatus.Success)
            return RequestResult<ApplicationResponse>.FromResult(updateResult);

        // Enviar notificação por email para o candidato (não bloqueante)
        try
        {
            var notify = await _notifyOnStatusChangedUseCase.ExecuteAsync(new NotifyOnApplicationStatusChangedRequest(updateResult.Data!.Id.Value), cancellationToken);
            if (notify.Status != EResultStatus.Success)
                _logger.LogWarning("Status da candidatura {ApplicationId} atualizado, mas o candidato não foi notificado: {Detail}",
                    updateResult.Data.Id.Value, notify.Message);
        }
        catch (Exception ex)
        {
            // Falha na notificação não impede a atualização, mas precisa ficar visível no log
            _logger.LogError(ex, "Falha ao notificar o candidato sobre a candidatura {ApplicationId}", updateResult.Data!.Id.Value);
        }

        return RequestResult<ApplicationResponse>.Success(MapToResponse(updateResult.Data!));
    }


    public static bool IsAllowedTransition(EApplicationStatus current, EApplicationStatus next) =>
        current switch
        {
            EApplicationStatus.Sent => next is EApplicationStatus.Viewed or EApplicationStatus.Selected or EApplicationStatus.Rejected or EApplicationStatus.Archived,
            EApplicationStatus.Viewed => next is EApplicationStatus.Selected or EApplicationStatus.Rejected or EApplicationStatus.Archived,
            EApplicationStatus.Selected or EApplicationStatus.Rejected => next is EApplicationStatus.Archived,
            _ => false
        };

    private static ApplicationResponse MapToResponse(Application app) => new()
    {
        Id = app.Id.Value,
        CandidateId = app.CandidateId.Value,
        JobId = app.JobId.Value,
        Status = app.Status.ToString(),
        AppliedAt = app.AppliedAt,
        ResumeId = app.ResumeId,
        ResumeSnapshot = ApplicationResumeSnapshotMapper.Deserialize(app.ResumeSnapshotJson),
        ViewedAt = app.ViewedAt,
        SelectedAt = app.SelectedAt,
        RejectedAt = app.RejectedAt,
        Notes = app.Notes,
        CreatedAt = app.CreatedAt
    };
}
