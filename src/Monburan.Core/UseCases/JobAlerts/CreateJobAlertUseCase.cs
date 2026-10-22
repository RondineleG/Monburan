using Monburan.Core.DTOs.Request;
using Monburan.Core.DTOs.Response;
using Monburan.Core.Entities;
using Monburan.Core.Enums;
using Monburan.Core.Interfaces.Repositories;
using Monburan.Core.Interfaces.UseCases;
using Monburan.Core.Requests;
using Monburan.Core.Services;
using Monburan.Core.ValueObjects;

namespace Monburan.Core.UseCases.JobAlerts;

public sealed record CreateJobAlertCommand(Guid CandidateId, JobAlertRequest Alert);

/// <summary>Cria um alerta de vaga para o candidato. O limite de 10 responde conflito (409).</summary>
public class CreateJobAlertUseCase : IUseCase<CreateJobAlertCommand, JobAlertResponse>
{
    public const int MaxAlertsPerCandidate = 10;

    private readonly IJobAlertRepository _alerts;

    public CreateJobAlertUseCase(IJobAlertRepository alerts)
    {
        _alerts = alerts;
    }

    public async Task<RequestResult<JobAlertResponse>> ExecuteAsync(CreateJobAlertCommand request, CancellationToken cancellationToken = default)
    {
        var errors = JobAlertInputValidation.Validate(request.Alert);
        if (errors.Count != 0)
            return RequestResult<JobAlertResponse>.WithValidations(errors);

        var count = await _alerts.CountByCandidateAsync(request.CandidateId, cancellationToken);
        if (count.Status != EResultStatus.Success)
            return RequestResult<JobAlertResponse>.FromResult(count);

        if (count.Data >= MaxAlertsPerCandidate)
        {
            return RequestResult<JobAlertResponse>.EntityAlreadyExists(
                "JobAlert", MaxAlertsPerCandidate,
                $"Você já tem {MaxAlertsPerCandidate} alertas de vaga. Exclua um alerta para criar outro.");
        }

        var alert = new JobAlert
        {
            Id = Id.Create(Guid.NewGuid()),
            CandidateId = Id.Create(request.CandidateId),
            Name = request.Alert.Name.Trim(),
            Keywords = request.Alert.Keywords?.Trim(),
            City = request.Alert.City?.Trim(),
            State = request.Alert.State?.Trim().ToUpperInvariant(),
            Mode = JobModes.Normalize(request.Alert.Mode),
            Category = request.Alert.Category?.Trim(),
            SalaryMin = request.Alert.SalaryMin,
            IsActive = request.Alert.IsActive ?? true,
            TenantId = "default"
        };

        var result = await _alerts.CreateAsync(alert, cancellationToken);
        if (result.Status != EResultStatus.Success)
            return RequestResult<JobAlertResponse>.FromResult(result);

        return RequestResult<JobAlertResponse>.Success(JobAlertMapper.ToResponse(result.Data!));
    }
}
