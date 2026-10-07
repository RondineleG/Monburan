using Monburan.Core.DTOs.Request;
using Monburan.Core.DTOs.Response;
using Monburan.Core.Entities;
using Monburan.Core.Enums;
using Monburan.Core.Interfaces.Repositories.Base;
using Monburan.Core.Interfaces.UseCases;
using Monburan.Core.Requests;

namespace Monburan.Core.UseCases.Advertisers;

public sealed class ListAdvertisersUseCase : IUseCase<ListAdvertisersRequest, ListAdvertisersResponse>
{
    private readonly IBaseRepository<Advertiser> _advertiserRepository;

    public ListAdvertisersUseCase(IBaseRepository<Advertiser> advertiserRepository)
    {
        _advertiserRepository = advertiserRepository;
    }

    public async Task<RequestResult<ListAdvertisersResponse>> ExecuteAsync(ListAdvertisersRequest request)
    {
        var result = await _advertiserRepository.GetAllAsync(request.Page, request.Size);
        if (result.Status != EResultStatus.Success)
            return RequestResult<ListAdvertisersResponse>.WithError(result.Message);

        var advertisers = result.Data!
            .Where(a => ShouldInclude(a, request))
            .Select(MapToResponse)
            .ToList();

        var response = new ListAdvertisersResponse
        {
            Items = advertisers,
            TotalCount = advertisers.Count,
            Page = request.Page,
            Size = request.Size
        };

        return RequestResult<ListAdvertisersResponse>.Success(response);
    }

    private static bool ShouldInclude(Advertiser advertiser, ListAdvertisersRequest request)
    {
        if (!string.IsNullOrWhiteSpace(request.SearchTerm))
        {
            var term = request.SearchTerm.ToLower();
            if (!advertiser.Name.Contains(term, StringComparison.OrdinalIgnoreCase) &&
                !advertiser.Email.Contains(term, StringComparison.OrdinalIgnoreCase) &&
                !(advertiser.TaxId?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false))
            {
                return false;
            }
        }

        if (!string.IsNullOrWhiteSpace(request.Status) &&
            Enum.TryParse<EAdvertiserStatus>(request.Status, true, out var statusFilter) &&
            advertiser.Status != statusFilter)
        {
            return false;
        }

        return true;
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
