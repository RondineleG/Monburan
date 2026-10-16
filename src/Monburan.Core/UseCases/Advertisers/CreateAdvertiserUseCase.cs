using Monburan.Core.DTOs.Request;
using Monburan.Core.DTOs.Response;
using Monburan.Core.Entities;
using Monburan.Core.Enums;
using Monburan.Core.Interfaces.Repositories.Base;
using Monburan.Core.Interfaces.UseCases;
using Monburan.Core.Requests;
using Monburan.Core.ValueObjects;

namespace Monburan.Core.UseCases.Advertisers;

public class CreateAdvertiserUseCase : IUseCase<CreateAdvertiserRequest, AdvertiserResponse>
{
    private readonly IBaseRepository<Advertiser> _advertiserRepository;

    public CreateAdvertiserUseCase(IBaseRepository<Advertiser> advertiserRepository)
    {
        _advertiserRepository = advertiserRepository;
    }

    public async Task<RequestResult<AdvertiserResponse>> ExecuteAsync(CreateAdvertiserRequest request)
    {
        var advertiser = new Advertiser
        {
            Id = Id.Create(Guid.NewGuid()),
            Name = request.Name,
            Email = request.Email,
            AddressId = request.AddressId.HasValue ? Id.Create(request.AddressId.Value) : null,
            Phone = request.Phone,
            TaxId = request.TaxId,
            PersonType = request.PersonType,
            CompanyDescription = request.CompanyDescription,
            Website = request.Website,
            LogoUrl = request.LogoUrl,
            Status = EAdvertiserStatus.Incomplete,
            IsEmailVerified = false,
            IsPhoneVerified = false,
            ActivePlan = "Free",
            TenantId = "default"
        };

        var result = await _advertiserRepository.CreateAsync(advertiser);
        if (result.Status != EResultStatus.Success)
            return RequestResult<AdvertiserResponse>.FromResult(result);

        return RequestResult<AdvertiserResponse>.Success(MapToResponse(result.Data!));
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
        ActivePlan = a.ActivePlan,
        CreatedAt = a.CreatedAt
    };
}
