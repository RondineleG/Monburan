using Microsoft.Extensions.Logging;

using Monburan.Core.DTOs.Request;
using Monburan.Core.DTOs.Response;
using Monburan.Core.Entities;
using Monburan.Core.Enums;
using Monburan.Core.Interfaces.Repositories;
using Monburan.Core.Interfaces.Repositories.Base;
using Monburan.Core.Interfaces.UseCases;
using Monburan.Core.Requests;
using Monburan.Core.UseCases.Notifications;
using Monburan.Core.ValueObjects;

namespace Monburan.Core.UseCases.Applications;

public class UpdateApplicationStatusUseCase : IUseCase<UpdateApplicationStatusRequest, ApplicationResponse>
{
    private readonly IBaseRepository<Application> _applicationRepository;
    private readonly IApplicationStatusHistoryRepository _historyRepository;
    private readonly NotifyOnApplicationStatusChangedUseCase _notifyOnStatusChangedUseCase;
    private readonly ILogger<UpdateApplicationStatusUseCase> _logger;

    public UpdateApplicationStatusUseCase(
        IBaseRepository<Application> applicationRepository,
        IApplicationStatusHistoryRepository historyRepository,
        NotifyOnApplicationStatusChangedUseCase notifyOnStatusChangedUseCase,
        ILogger<UpdateApplicationStatusUseCase> logger)
    {
        _applicationRepository = applicationRepository;
        _historyRepository = historyRepository;
        _notifyOnStatusChangedUseCase = notifyOnStatusChangedUseCase;
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

        var updateResult = await _applicationRepository.UpdateAsync(application, cancellationToken);
        if (updateResult.Status != EResultStatus.Success)
            return RequestResult<ApplicationResponse>.FromResult(updateResult);

        // Histórico só registra mudança real de etapa (não bloqueante: a mudança já está gravada)
        if (oldStatus != application.Status)
            await TryWriteHistoryAsync(application, request.Notes, request.Responsible);

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

    // Falha na gravação do histórico fica no log: a trilha é um extra e não pode derrubar a mudança de etapa
    private async Task TryWriteHistoryAsync(Application application, string? note, string? responsible, CancellationToken cancellationToken = default)
    {
        try
        {
            var historyResult = await _historyRepository.CreateAsync(new ApplicationStatusHistory
            {
                Id = Id.Create(Guid.NewGuid()),
                ApplicationId = application.Id,
                Status = application.Status,
                Date = DateTime.UtcNow,
                Note = note,
                Responsible = responsible,
                TenantId = application.TenantId
            },
            cancellationToken);

            if (historyResult.Status != EResultStatus.Success)
                _logger.LogWarning("Histórico da candidatura {ApplicationId} não foi gravado: {Detail}",
                    application.Id.Value, historyResult.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha ao gravar o histórico da candidatura {ApplicationId}", application.Id.Value);
        }
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
