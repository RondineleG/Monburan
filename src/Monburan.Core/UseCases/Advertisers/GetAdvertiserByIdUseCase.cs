using Monburan.Core.DTOs.Response;
using Monburan.Core.Entities;
using Monburan.Core.Enums;
using Monburan.Core.Interfaces.Repositories.Base;
using Monburan.Core.Interfaces.UseCases;
using Monburan.Core.Requests;
using Monburan.Core.ValueObjects;

namespace Monburan.Core.UseCases.Advertisers;

public sealed class GetAdvertiserByIdUseCase : IUseCase<Guid, AdvertiserResponse>
{
    private readonly IBaseRepository<Advertiser> _advertiserRepository;

    public GetAdvertiserByIdUseCase(IBaseRepository<Advertiser> advertiserRepository)
    {
        _advertiserRepository = advertiserRepository;
    }

    public async Task<RequestResult<AdvertiserResponse>> ExecuteAsync(Guid request)
    {
        var result = await _advertiserRepository.GetByIdAsync(Id.Create(request));
        if (result.Status != EResultStatus.Success)
            return RequestResult<AdvertiserResponse>.FromResult(result);

        var advertiser = result.Data!;
        var response = MapToResponse(advertiser);

        return RequestResult<AdvertiserResponse>.Success(response);
    }

    private static AdvertiserResponse MapToResponse(Advertiser a) => new()
    {
        Id = a.Id.Value,
        Name = a.Name,
        Email = a.Email,
        Phone = a.Phone,
        TaxId = a.TaxId,
        PersonType = a.PersonType.ToString(),
        CompanyDescription = a.CompanyDescription,
        Website = a.Website,
        LogoUrl = a.LogoUrl,
        Status = a.Status.ToString(),
        IsEmailVerified = a.IsEmailVerified,
        IsPhoneVerified = a.IsPhoneVerified,
        ActivePlan = a.ActivePlan,
        City = a.Address?.City,
        State = a.Address?.State,
        JobsCount = a.Jobs?.Count ?? 0,
        CreatedAt = a.CreatedAt,
        UpdatedAt = a.UpdatedAt
    };
}
