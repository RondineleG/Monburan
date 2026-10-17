using Microsoft.Extensions.Logging;

using Monburan.Core.DTOs.Request;
using Monburan.Core.DTOs.Response;
using Monburan.Core.Entities;
using Monburan.Core.Enums;
using Monburan.Core.Interfaces.Repositories.Base;
using Monburan.Core.Interfaces.UseCases;
using Monburan.Core.Requests;
using Monburan.Core.UseCases.Notifications;
using Monburan.Core.ValueObjects;

namespace Monburan.Core.UseCases.Applications;

public class UpdateApplicationStatusUseCase : IUseCase<UpdateApplicationStatusRequest, ApplicationResponse>
{
    private readonly IBaseRepository<Application> _applicationRepository;
    private readonly NotifyOnApplicationStatusChangedUseCase _notifyOnStatusChangedUseCase;
    private readonly ILogger<UpdateApplicationStatusUseCase> _logger;

    public UpdateApplicationStatusUseCase(
        IBaseRepository<Application> applicationRepository,
        NotifyOnApplicationStatusChangedUseCase notifyOnStatusChangedUseCase,
        ILogger<UpdateApplicationStatusUseCase> logger)
    {
        _applicationRepository = applicationRepository;
        _notifyOnStatusChangedUseCase = notifyOnStatusChangedUseCase;
        _logger = logger;
    }

    public async Task<RequestResult<ApplicationResponse>> ExecuteAsync(UpdateApplicationStatusRequest request)
    {
        var result = await _applicationRepository.GetByIdAsync(Id.Create(request.ApplicationId));
        if (result.Status != EResultStatus.Success)
            return RequestResult<ApplicationResponse>.EntityNotFound("Application", request.ApplicationId, "Candidatura não encontrada.");

        var application = result.Data!;

        if (application.Job == null || application.Job.AdvertiserId.Value != request.AdvertiserId)
            return RequestResult<ApplicationResponse>.EntityNotFound("Application", request.ApplicationId, "Candidatura não encontrada.");

        var oldStatus = application.Status;

        application.Status = request.NewStatus;

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

        var updateResult = await _applicationRepository.UpdateAsync(application);
        if (updateResult.Status != EResultStatus.Success)
            return RequestResult<ApplicationResponse>.FromResult(updateResult);

        // Enviar notificação por email para o candidato (não bloqueante)
        try
        {
            var notify = await _notifyOnStatusChangedUseCase.ExecuteAsync(new NotifyOnApplicationStatusChangedRequest(updateResult.Data!.Id.Value));
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

    private static ApplicationResponse MapToResponse(Application app) => new()
    {
        Id = app.Id.Value,
        CandidateId = app.CandidateId.Value,
        JobId = app.JobId.Value,
        Status = app.Status.ToString(),
        AppliedAt = app.AppliedAt,
        ResumeId = app.ResumeId,
        ViewedAt = app.ViewedAt,
        SelectedAt = app.SelectedAt,
        RejectedAt = app.RejectedAt,
        Notes = app.Notes,
        CreatedAt = app.CreatedAt
    };
}
