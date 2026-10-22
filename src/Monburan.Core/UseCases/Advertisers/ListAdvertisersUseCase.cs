using Monburan.Core.DTOs.Request;
using Monburan.Core.DTOs.Response;
using Monburan.Core.Entities;
using Monburan.Core.Enums;
using Monburan.Core.Interfaces.Repositories;
using Monburan.Core.Interfaces.UseCases;
using Monburan.Core.Requests;

namespace Monburan.Core.UseCases.Advertisers;

public sealed class ListAdvertisersUseCase : IUseCase<ListAdvertisersRequest, ListAdvertisersResponse>
{
    private readonly IAdvertiserRepository _advertiserRepository;

    public ListAdvertisersUseCase(IAdvertiserRepository advertiserRepository)
    {
        _advertiserRepository = advertiserRepository;
    }

    public async Task<RequestResult<ListAdvertisersResponse>> ExecuteAsync(ListAdvertisersRequest request, CancellationToken cancellationToken = default)
    {
        var page = request.Page < 1 ? 1 : request.Page;
        var size = request.Size is < 1 or > 100 ? 20 : request.Size;
        EAdvertiserStatus? status = Enum.TryParse<EAdvertiserStatus>(request.Status, true, out var parsed) ? parsed : null;

        var result = await _advertiserRepository.SearchAsync(request.SearchTerm, status, page, size, cancellationToken: cancellationToken);
        if (result.Status != EResultStatus.Success || result.Data is null)
            return RequestResult<ListAdvertisersResponse>.FromResult(result);

        var response = new ListAdvertisersResponse
        {
            Items = result.Data.Data.Select(MapToResponse).ToList(),
            TotalCount = result.Data.TotalCount,
            Page = page,
            Size = size
        };

        return RequestResult<ListAdvertisersResponse>.Success(response);
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
