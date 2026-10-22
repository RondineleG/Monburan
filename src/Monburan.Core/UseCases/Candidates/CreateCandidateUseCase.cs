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

namespace Monburan.Core.UseCases.Candidates;

public sealed class CreateCandidateUseCase : IUseCase<CreateCandidateRequest, CandidateResponse>
{
    private readonly IAccountProfileRepository _profileRepository;
    private readonly IEntityValidator<Candidate> _candidateValidator;

    public CreateCandidateUseCase(
        IAccountProfileRepository profileRepository,
        IEntityValidator<Candidate> candidateValidator)
    {
        _profileRepository = profileRepository;
        _candidateValidator = candidateValidator;
    }

    public async Task<RequestResult<CandidateResponse>> ExecuteAsync(CreateCandidateRequest request, CancellationToken cancellationToken = default)
    {
        var candidate = new Candidate
        {
            Id = Id.Create(Guid.NewGuid()),
            Name = request.Name,
            Email = request.Email,
            AddressId = request.AddressId.HasValue ? Id.Create(request.AddressId.Value) : null,
            Phone = request.Phone,
            DateOfBirth = request.DateOfBirth,
            LinkedIn = request.LinkedIn,
            Status = EAccountStatus.Incomplete,
            IsEmailVerified = false,
            IsPhoneVerified = false,
            TenantId = "default"
        };

        // Valida antes de gravar: nome muito curto e e-mail inválido não entram no banco.
        var validationResult = _candidateValidator.Validate(candidate);
        if (!validationResult.IsValid)
        {
            // O cadastro (web e API) cria o perfil apenas com nome e e-mail — os demais
            // campos são completados depois. Por isso só bloqueiam a criação os erros
            // dos campos realmente enviados no pedido; os ausentes são validados na
            // atualização do perfil, quando o candidato completa os dados.
            var blockingErrors = validationResult.Errors
                .Where(error => IsFieldInformedOnCreate(error.Field, request))
                .ToArray();

            if (blockingErrors.Length > 0)
                return ToValidationError(blockingErrors);
        }

        var result = await _profileRepository.CreateCandidateAsync(candidate, cancellationToken);
        if (result.Status != EResultStatus.Success)
            return RequestResult<CandidateResponse>.FromResult(result);

        return RequestResult<CandidateResponse>.Success(MapToResponse(result.Data!));
    }

    private static CandidateResponse MapToResponse(Candidate c) => new()
    {
        Id = c.Id.Value,
        Name = c.Name,
        Email = c.Email,
        Phone = c.Phone,
        DateOfBirth = c.DateOfBirth,
        LinkedIn = c.LinkedIn,
        Status = c.Status.ToString(),
        IsEmailVerified = c.IsEmailVerified,
        CreatedAt = c.CreatedAt
    };

    // Nome e e-mail são obrigatórios no pedido e sempre validados; os campos
    // opcionais só entram na validação quando informados no cadastro.
    private static bool IsFieldInformedOnCreate(string field, CreateCandidateRequest request) => field switch
    {
        "Name" or "Email" => true,
        "Phone" => !string.IsNullOrWhiteSpace(request.Phone),
        "DateOfBirth" => request.DateOfBirth.HasValue,
        "LinkedIn" => !string.IsNullOrWhiteSpace(request.LinkedIn),
        _ => false
    };

    // Todos os erros de validação vão para o resultado — o formulário da API e o
    // da web apontam o campo correspondente de cada um.
    private static RequestResult<CandidateResponse> ToValidationError(IReadOnlyCollection<ValidationErrorMessage> errors)
    {
        var firstError = errors.First();
        var result = RequestResult<CandidateResponse>.WithValidationError(firstError.Message, firstError.Field);

        foreach (var error in errors.Skip(1))
            result.ValidationResult.AddError(error.Message, error.Field);

        return result;
    }
}
