using Monburan.Core.Enums;
using Monburan.Core.Interfaces.Repositories;
using Monburan.Core.Interfaces.UseCases;
using Monburan.Core.Requests;

namespace Monburan.Core.UseCases.Jobs;

public sealed record UnsaveJobRequest(Guid CandidateId, Guid JobId);

/// <summary>Remove a vaga salva. Idempotente: remover o que não está salvo responde sucesso.</summary>
public class UnsaveJobUseCase : IUseCase<UnsaveJobRequest, bool>
{
    private readonly ISavedJobRepository _savedJobs;

    public UnsaveJobUseCase(ISavedJobRepository savedJobs)
    {
        _savedJobs = savedJobs;
    }

    public async Task<RequestResult<bool>> ExecuteAsync(UnsaveJobRequest request, CancellationToken cancellationToken = default)
    {
        var result = await _savedJobs.RemoveAsync(request.CandidateId, request.JobId, cancellationToken);
        if (result.Status != EResultStatus.Success)
            return RequestResult<bool>.FromResult(result);

        return RequestResult<bool>.Success(true);
    }
}
