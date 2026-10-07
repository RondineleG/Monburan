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

    public UpdateApplicationStatusUseCase(
        IBaseRepository<Application> applicationRepository,
        NotifyOnApplicationStatusChangedUseCase notifyOnStatusChangedUseCase)
    {
        _applicationRepository = applicationRepository;
        _notifyOnStatusChangedUseCase = notifyOnStatusChangedUseCase;
    }

    public async Task<RequestResult<ApplicationResponse>> ExecuteAsync(UpdateApplicationStatusRequest request)
    {
        var result = await _applicationRepository.GetByIdAsync(Id.Create(request.ApplicationId));
        if (result.Status != EResultStatus.Success)
            return RequestResult<ApplicationResponse>.WithError("Candidatura não encontrada.");

        var application = result.Data!;

        if (application.Job == null || application.Job.AdvertiserId.Value != request.AdvertiserId)
            return RequestResult<ApplicationResponse>.WithError("Candidatura não encontrada.");

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
            return RequestResult<ApplicationResponse>.WithError(updateResult.Message);

        // Enviar notificação por email para o candidato (não bloqueante)
        try
        {
            await _notifyOnStatusChangedUseCase.ExecuteAsync(new NotifyOnApplicationStatusChangedRequest(updateResult.Data!.Id.Value));
        }
        catch (Exception)
        {
            // Falha na notificação não deve impedir a atualização do status
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
        ViewedAt = app.ViewedAt,
        SelectedAt = app.SelectedAt,
        RejectedAt = app.RejectedAt,
        Notes = app.Notes,
        CreatedAt = app.CreatedAt
    };
}
