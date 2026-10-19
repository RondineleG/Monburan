using Monburan.Core.DTOs.Request;
using Monburan.Core.DTOs.Response;
using Monburan.Core.Entities;
using Monburan.Core.Enums;
using Monburan.Core.Interfaces.Repositories.Base;
using Monburan.Core.Interfaces.UseCases;
using Monburan.Core.Interfaces.Validations;
using Monburan.Core.Requests;
using Monburan.Core.Validations;
using Monburan.Core.ValueObjects;

namespace Monburan.Core.UseCases.Advertisers;

public sealed class UpdateAdvertiserUseCase : IUseCase<UpdateAdvertiserRequest, AdvertiserResponse>
{
    private readonly IBaseRepository<Advertiser> _advertiserRepository;
    private readonly IEntityValidator<Advertiser> _advertiserValidator;

    public UpdateAdvertiserUseCase(
        IBaseRepository<Advertiser> advertiserRepository,
        IEntityValidator<Advertiser> advertiserValidator)
    {
        _advertiserRepository = advertiserRepository;
        _advertiserValidator = advertiserValidator;
    }

    public async Task<RequestResult<AdvertiserResponse>> ExecuteAsync(UpdateAdvertiserRequest request)
    {
        var getResult = await _advertiserRepository.GetByIdAsync(Id.Create(request.Id));

        if (getResult.Status != EResultStatus.Success || getResult.Data == null)
            return RequestResult<AdvertiserResponse>.EntityNotFound("Advertiser", request.Id, "Anunciante não encontrado.");

        var advertiser = getResult.Data;

        // Endereço já vinculado ao perfil antes da edição: é a única referência de
        // posse do anunciante (mesma regra usada por AddressRepository.GetOwnedIdsAsync).
        var ownedAddressId = advertiser.AddressId;

        advertiser.Name = request.Name;
        advertiser.Phone = request.Phone;
        advertiser.TaxId = request.TaxId;
        advertiser.CompanyDescription = request.CompanyDescription;
        advertiser.Website = request.Website;
        advertiser.LogoUrl = request.LogoUrl;
        advertiser.PersonType = request.PersonType;

        if (request.AddressId.HasValue)
        {
            // A posse de um endereço é derivada exclusivamente de Advertiser.AddressId.
            // Sem esta checagem, quem soubesse o Guid do endereço de outra empresa
            // apontaria o próprio perfil para ele e leria rua, número, bairro e CEP
            // na listagem de endereços (IDOR).
            if (ownedAddressId is null || ownedAddressId.Value != request.AddressId.Value)
                return RequestResult<AdvertiserResponse>.WithValidationError(
                    "O endereço informado não pertence ao seu perfil.", nameof(request.AddressId));

            advertiser.AddressId = Id.Create(request.AddressId.Value);
        }

        // Valida antes de gravar: nome muito curto, telefone ausente e CNPJ/CPF
        // inválido não podem chegar ao banco.
        var validationResult = _advertiserValidator.ValidateUpdate(advertiser);
        if (!validationResult.IsValid)
        {
            // A atualização valida os campos que o pedido carrega. O perfil nasce
            // incompleto (o cadastro manda só nome, e-mail e tipo de pessoa) e os
            // clientes não enviam telefone/CNPJ de quem ainda não preencheu o
            // campo; exigir esses dados aqui deixaria a edição do perfil
            // impossível sem que o usuário pudesse corrigir.
            var blockingErrors = validationResult.Errors
                .Where(error => IsFieldInformedOnUpdate(error.Field, request))
                .ToArray();

            if (blockingErrors.Length > 0)
                return ToValidationError(blockingErrors);
        }

        var result = await _advertiserRepository.UpdateAsync(advertiser);

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

    // Nome e e-mail fazem parte do perfil salvo e são sempre validados; os demais
    // campos só entram na validação quando o pedido os traz.
    private static bool IsFieldInformedOnUpdate(string field, UpdateAdvertiserRequest request) => field switch
    {
        "Name" or "Email" => true,
        "Phone" => !string.IsNullOrWhiteSpace(request.Phone),
        "TaxId" => !string.IsNullOrWhiteSpace(request.TaxId),
        "Website" => !string.IsNullOrWhiteSpace(request.Website),
        _ => false
    };

    // Todos os erros de validação vão para o resultado — o formulário da API e o
    // da web apontam o campo correspondente de cada um.
    private static RequestResult<AdvertiserResponse> ToValidationError(IReadOnlyCollection<ValidationErrorMessage> errors)
    {
        var firstError = errors.First();
        var result = RequestResult<AdvertiserResponse>.WithValidationError(firstError.Message, firstError.Field);

        foreach (var error in errors.Skip(1))
            result.ValidationResult.AddError(error.Message, error.Field);

        return result;
    }
}
