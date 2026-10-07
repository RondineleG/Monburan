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

    public async Task<RequestResult<bool>> ExecuteAsync(Guid request)
    {
        var result = await _jobRepository.DeleteAsync(Id.Create(request));
        if (result.Status != EResultStatus.Success)
            return RequestResult<bool>.WithError(result.Message);

        return RequestResult<bool>.Success(true);
    }
}
