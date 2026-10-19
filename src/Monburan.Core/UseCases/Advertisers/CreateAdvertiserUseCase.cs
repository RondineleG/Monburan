using Monburan.Core.DTOs.Request;
using Monburan.Core.DTOs.Response;
using Monburan.Core.Entities;
using Monburan.Core.Enums;
using Monburan.Core.Interfaces.Repositories;
using Monburan.Core.Interfaces.UseCases;
using Monburan.Core.Interfaces.Validations;
using Monburan.Core.Requests;
using Monburan.Core.Validations;
using Monburan.Core.ValueObjects;

namespace Monburan.Core.UseCases.Advertisers;

public sealed class CreateAdvertiserUseCase : IUseCase<CreateAdvertiserRequest, AdvertiserResponse>
{
    private readonly IAccountProfileRepository _profileRepository;
    private readonly IEntityValidator<Advertiser> _advertiserValidator;

    public CreateAdvertiserUseCase(
        IAccountProfileRepository profileRepository,
        IEntityValidator<Advertiser> advertiserValidator)
    {
        _profileRepository = profileRepository;
        _advertiserValidator = advertiserValidator;
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

        // Valida antes de gravar: nome muito curto e e-mail inválido não entram no banco.
        var validationResult = _advertiserValidator.Validate(advertiser);
        if (!validationResult.IsValid)
        {
            // O cadastro (web e API) cria o perfil apenas com nome, e-mail e tipo de
            // pessoa — telefone e CNPJ/CPF são preenchidos depois. Por isso só bloqueiam
            // a criação os erros dos campos realmente enviados no pedido; os ausentes
            // são validados na atualização do perfil.
            var blockingErrors = validationResult.Errors
                .Where(error => IsFieldInformedOnCreate(error.Field, request))
                .ToArray();

            if (blockingErrors.Length > 0)
                return ToValidationError(blockingErrors);
        }

        var result = await _profileRepository.CreateAdvertiserAsync(advertiser);
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

    // Nome e e-mail são obrigatórios no pedido e sempre validados; os campos
    // opcionais só entram na validação quando informados no cadastro.
    private static bool IsFieldInformedOnCreate(string field, CreateAdvertiserRequest request) => field switch
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
