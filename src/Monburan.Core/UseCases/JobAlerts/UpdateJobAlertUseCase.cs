using Monburan.Core.DTOs.Request;
using Monburan.Core.DTOs.Response;
using Monburan.Core.Enums;
using Monburan.Core.Interfaces.Repositories;
using Monburan.Core.Interfaces.UseCases;
using Monburan.Core.Requests;
using Monburan.Core.Services;
using Monburan.Core.ValueObjects;

namespace Monburan.Core.UseCases.JobAlerts;

public sealed record UpdateJobAlertCommand(Guid CandidateId, Guid AlertId, JobAlertRequest Alert);

/// <summary>Edita um alerta do próprio candidato. Alerta de outra pessoa responde como inexistente.</summary>
public class UpdateJobAlertUseCase : IUseCase<UpdateJobAlertCommand, JobAlertResponse>
{
    private readonly IJobAlertRepository _alerts;

    public UpdateJobAlertUseCase(IJobAlertRepository alerts)
    {
        _alerts = alerts;
    }

    public async Task<RequestResult<JobAlertResponse>> ExecuteAsync(UpdateJobAlertCommand request)
    {
        var errors = JobAlertInputValidation.Validate(request.Alert);
        if (errors.Count != 0)
            return RequestResult<JobAlertResponse>.WithValidations(errors);

        var found = await _alerts.GetByIdAsync(Id.Create(request.AlertId));
        if (found.Status != EResultStatus.Success || found.Data is null
            || found.Data.CandidateId.Value != request.CandidateId)
        {
            return RequestResult<JobAlertResponse>.EntityNotFound("JobAlert", request.AlertId, "Alerta não encontrado.");
        }

        var alert = found.Data;
        alert.Name = request.Alert.Name.Trim();
        alert.Keywords = request.Alert.Keywords?.Trim();
        alert.City = request.Alert.City?.Trim();
        alert.State = request.Alert.State?.Trim().ToUpperInvariant();
        alert.Mode = JobModes.Normalize(request.Alert.Mode);
        alert.Category = request.Alert.Category?.Trim();
        alert.SalaryMin = request.Alert.SalaryMin;
        alert.IsActive = request.Alert.IsActive ?? alert.IsActive;

        var result = await _alerts.UpdateAsync(alert);
        if (result.Status != EResultStatus.Success)
            return RequestResult<JobAlertResponse>.FromResult(result);

        return RequestResult<JobAlertResponse>.Success(JobAlertMapper.ToResponse(result.Data!));
    }
}
