using Monburan.Core.Enums;
using Monburan.Core.Interfaces.Repositories;
using Monburan.Core.Interfaces.UseCases;
using Monburan.Core.Requests;
using Monburan.Core.ValueObjects;

namespace Monburan.Core.UseCases.JobAlerts;

public sealed record DeleteJobAlertCommand(Guid CandidateId, Guid AlertId);

/// <summary>Exclui um alerta do próprio candidato. Alerta de outra pessoa responde como inexistente.</summary>
public class DeleteJobAlertUseCase : IUseCase<DeleteJobAlertCommand, bool>
{
    private readonly IJobAlertRepository _alerts;

    public DeleteJobAlertUseCase(IJobAlertRepository alerts)
    {
        _alerts = alerts;
    }

    public async Task<RequestResult<bool>> ExecuteAsync(DeleteJobAlertCommand request)
    {
        var found = await _alerts.GetByIdAsync(Id.Create(request.AlertId));
        if (found.Status != EResultStatus.Success || found.Data is null
            || found.Data.CandidateId.Value != request.CandidateId)
        {
            return RequestResult<bool>.EntityNotFound("JobAlert", request.AlertId, "Alerta não encontrado.");
        }

        var result = await _alerts.DeleteAsync(found.Data.Id);
        if (result.Status != EResultStatus.Success)
            return RequestResult<bool>.FromResult(result);

        return RequestResult<bool>.Success(true);
    }
}
