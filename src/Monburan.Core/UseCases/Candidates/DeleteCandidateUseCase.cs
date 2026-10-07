using Monburan.Core.Entities;
using Monburan.Core.Enums;
using Monburan.Core.Interfaces.Repositories.Base;
using Monburan.Core.Interfaces.UseCases;
using Monburan.Core.Requests;
using Monburan.Core.ValueObjects;

namespace Monburan.Core.UseCases.Candidates;

public sealed class DeleteCandidateUseCase : IUseCase<Guid, bool>
{
    private readonly IBaseRepository<Candidate> _candidateRepository;

    public DeleteCandidateUseCase(IBaseRepository<Candidate> candidateRepository)
    {
        _candidateRepository = candidateRepository;
    }

    public async Task<RequestResult<bool>> ExecuteAsync(Guid request)
    {
        var result = await _candidateRepository.DeleteAsync(Id.Create(request));
        if (result.Status != EResultStatus.Success)
            return RequestResult<bool>.WithError(result.Message);

        return RequestResult<bool>.Success(true);
    }
}
