using Monburan.Core.DTOs.Response;
using Monburan.Core.Enums;
using Monburan.Core.Interfaces.Repositories;
using Monburan.Core.Interfaces.UseCases;
using Monburan.Core.Requests;

namespace Monburan.Core.UseCases.JobAlerts;

/// <summary>Alertas do candidato autenticado, mais recentes primeiro.</summary>
public class ListJobAlertsUseCase : IUseCase<Guid, IEnumerable<JobAlertResponse>>
{
    private readonly IJobAlertRepository _alerts;

    public ListJobAlertsUseCase(IJobAlertRepository alerts)
    {
        _alerts = alerts;
    }

    public async Task<RequestResult<IEnumerable<JobAlertResponse>>> ExecuteAsync(Guid request, CancellationToken cancellationToken = default)
    {
        var result = await _alerts.ListByCandidateAsync(request, cancellationToken: cancellationToken);
        if (result.Status != EResultStatus.Success || result.Data is null)
            return RequestResult<IEnumerable<JobAlertResponse>>.FromResult(result);

        return RequestResult<IEnumerable<JobAlertResponse>>.Success(
            result.Data.Select(JobAlertMapper.ToResponse).ToList());
    }
}
