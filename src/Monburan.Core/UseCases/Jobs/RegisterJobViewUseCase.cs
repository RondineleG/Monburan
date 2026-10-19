using Monburan.Core.Enums;
using Monburan.Core.Interfaces.Repositories;
using Monburan.Core.Interfaces.UseCases;
using Monburan.Core.Requests;
using Monburan.Core.ValueObjects;

namespace Monburan.Core.UseCases.Jobs;

public sealed record RegisterJobViewRequest(Guid JobId, Guid? ViewerId);

/// <summary>Conta a visualização da vaga. A visita do próprio anunciante não incrementa o contador.</summary>
public class RegisterJobViewUseCase : IUseCase<RegisterJobViewRequest, bool>
{
    private readonly IJobRepository _jobs;

    public RegisterJobViewUseCase(IJobRepository jobs)
    {
        _jobs = jobs;
    }

    public async Task<RequestResult<bool>> ExecuteAsync(RegisterJobViewRequest request)
    {
        var jobResult = await _jobs.GetByIdAsync(Id.Create(request.JobId));
        if (jobResult.Status != EResultStatus.Success || jobResult.Data is null)
            return RequestResult<bool>.EntityNotFound("Job", request.JobId, "Vaga não encontrada.");

        if (request.ViewerId.HasValue && jobResult.Data.AdvertiserId.Value == request.ViewerId.Value)
            return RequestResult<bool>.Success(true);

        return await _jobs.IncrementViewsAsync(request.JobId);
    }
}
