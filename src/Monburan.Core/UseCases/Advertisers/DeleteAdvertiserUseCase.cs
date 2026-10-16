using Monburan.Core.Entities;
using Monburan.Core.Enums;
using Monburan.Core.Interfaces.Repositories.Base;
using Monburan.Core.Interfaces.UseCases;
using Monburan.Core.Requests;
using Monburan.Core.ValueObjects;

namespace Monburan.Core.UseCases.Advertisers;

public sealed class DeleteAdvertiserUseCase : IUseCase<Guid, bool>
{
    private readonly IBaseRepository<Advertiser> _advertiserRepository;

    public DeleteAdvertiserUseCase(IBaseRepository<Advertiser> advertiserRepository)
    {
        _advertiserRepository = advertiserRepository;
    }

    public async Task<RequestResult<bool>> ExecuteAsync(Guid request)
    {
        var result = await _advertiserRepository.DeleteAsync(Id.Create(request));
        if (result.Status != EResultStatus.Success)
            return RequestResult<bool>.FromResult(result);

        return RequestResult<bool>.Success(true);
    }
}
