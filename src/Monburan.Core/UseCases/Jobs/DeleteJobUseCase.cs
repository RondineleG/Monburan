using Monburan.Core.Entities;
using Monburan.Core.Enums;
using Monburan.Core.Interfaces.Repositories.Base;
using Monburan.Core.Interfaces.UseCases;
using Monburan.Core.Requests;
using Monburan.Core.ValueObjects;

namespace Monburan.Core.UseCases.Jobs;

public class DeleteJobUseCase : IUseCase<Guid, bool>
{
    private readonly IBaseRepository<Job> _jobRepository;

    public DeleteJobUseCase(IBaseRepository<Job> jobRepository)
    {
        _jobRepository = jobRepository;
    }

    public async Task<RequestResult<bool>> ExecuteAsync(Guid request, CancellationToken cancellationToken = default)
    {
        var result = await _jobRepository.DeleteAsync(Id.Create(request), cancellationToken);
        if (result.Status != EResultStatus.Success)
            return RequestResult<bool>.FromResult(result);

        return RequestResult<bool>.Success(true);
    }
}
