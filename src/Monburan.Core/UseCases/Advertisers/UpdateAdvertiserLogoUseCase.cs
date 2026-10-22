using Monburan.Core.DTOs.Response;
using Monburan.Core.Entities;
using Monburan.Core.Enums;
using Monburan.Core.Interfaces.Repositories.Base;
using Monburan.Core.Interfaces.UseCases;
using Monburan.Core.Requests;
using Monburan.Core.ValueObjects;

namespace Monburan.Core.UseCases.Advertisers;

public sealed record UpdateAdvertiserLogoRequest(Guid AdvertiserId, string LogoUrl);

/// <summary>
/// Atualiza apenas a logo do anunciante — o upload não deve depender de o restante
/// do perfil (telefone, CPF/CNPJ) estar completo.
/// </summary>
public sealed class UpdateAdvertiserLogoUseCase : IUseCase<UpdateAdvertiserLogoRequest, AdvertiserResponse>
{
    private readonly IBaseRepository<Advertiser> _advertiserRepository;

    public UpdateAdvertiserLogoUseCase(IBaseRepository<Advertiser> advertiserRepository)
    {
        _advertiserRepository = advertiserRepository;
    }

    public async Task<RequestResult<AdvertiserResponse>> ExecuteAsync(UpdateAdvertiserLogoRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.LogoUrl))
            return RequestResult<AdvertiserResponse>.WithValidationError("Envie uma imagem para a logo.", nameof(request.LogoUrl));

        var getResult = await _advertiserRepository.GetByIdAsync(Id.Create(request.AdvertiserId), cancellationToken);
        if (getResult.Status != EResultStatus.Success || getResult.Data is null)
            return RequestResult<AdvertiserResponse>.EntityNotFound("Advertiser", request.AdvertiserId, "Anunciante não encontrado.");

        var advertiser = getResult.Data;
        advertiser.LogoUrl = request.LogoUrl;

        var result = await _advertiserRepository.UpdateAsync(advertiser, cancellationToken);
        if (result.Status != EResultStatus.Success)
            return RequestResult<AdvertiserResponse>.FromResult(result);

        return RequestResult<AdvertiserResponse>.Success(new AdvertiserResponse
        {
            Id = advertiser.Id.Value,
            Name = advertiser.Name,
            Email = advertiser.Email,
            Phone = advertiser.Phone,
            TaxId = advertiser.TaxId,
            PersonType = advertiser.PersonType.ToString(),
            CompanyDescription = advertiser.CompanyDescription,
            Website = advertiser.Website,
            LogoUrl = advertiser.LogoUrl,
            Status = advertiser.Status.ToString(),
            IsEmailVerified = advertiser.IsEmailVerified,
            ActivePlan = advertiser.ActivePlan,
            CreatedAt = advertiser.CreatedAt
        });
    }
}
