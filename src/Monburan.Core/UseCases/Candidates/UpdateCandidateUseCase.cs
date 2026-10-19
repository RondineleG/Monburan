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

namespace Monburan.Core.UseCases.Candidates;

public sealed class UpdateCandidateUseCase : IUseCase<UpdateCandidateRequest, CandidateResponse>
{
    private readonly IBaseRepository<Candidate> _candidateRepository;
    private readonly IEntityValidator<Candidate> _candidateValidator;

    public UpdateCandidateUseCase(
        IBaseRepository<Candidate> candidateRepository,
        IEntityValidator<Candidate> candidateValidator)
    {
        _candidateRepository = candidateRepository;
        _candidateValidator = candidateValidator;
    }

    public async Task<RequestResult<CandidateResponse>> ExecuteAsync(UpdateCandidateRequest request)
    {
        var getResult = await _candidateRepository.GetByIdAsync(Id.Create(request.Id));

        if (getResult.Status != EResultStatus.Success || getResult.Data == null)
            return RequestResult<CandidateResponse>.EntityNotFound("Candidate", request.Id, "Candidato não encontrado.");

        var candidate = getResult.Data;

        // Endereço já vinculado ao perfil antes da edição: é a única referência de
        // posse do candidato (mesma regra usada por AddressRepository.GetOwnedIdsAsync).
        var ownedAddressId = candidate.AddressId;

        candidate.Name = request.Name;
        candidate.Phone = request.Phone;
        candidate.DateOfBirth = request.DateOfBirth;
        candidate.LinkedIn = request.LinkedIn;

        if (request.AddressId.HasValue)
        {
            // A posse de um endereço é derivada exclusivamente de Candidate.AddressId.
            // Sem esta checagem, quem soubesse o Guid do endereço de outra pessoa
            // apontaria o próprio perfil para ele e leria rua, número, bairro e CEP
            // na listagem de endereços (IDOR).
            if (ownedAddressId is null || ownedAddressId.Value != request.AddressId.Value)
                return RequestResult<CandidateResponse>.WithValidationError(
                    "O endereço informado não pertence ao seu perfil.", nameof(request.AddressId));

            candidate.AddressId = Id.Create(request.AddressId.Value);
        }

        // Valida antes de gravar: nome muito curto, e-mail inválido e data de
        // nascimento de menor de 14 anos não podem chegar ao banco.
        var validationResult = _candidateValidator.ValidateUpdate(candidate);
        if (!validationResult.IsValid)
        {
            // A atualização valida os campos que o pedido carrega. O perfil nasce
            // incompleto (o cadastro manda só nome e e-mail) e os clientes — o
            // aplicativo mobile, por exemplo — não enviam a data de nascimento de
            // quem ainda não preencheu o campo; exigir esse dado aqui deixaria a
            // edição do perfil impossível sem que o usuário pudesse corrigir.
            var blockingErrors = validationResult.Errors
                .Where(error => IsFieldInformedOnUpdate(error.Field, request))
                .ToArray();

            if (blockingErrors.Length > 0)
                return ToValidationError(blockingErrors);
        }

        var result = await _candidateRepository.UpdateAsync(candidate);

        if (result.Status != EResultStatus.Success)
            return RequestResult<CandidateResponse>.FromResult(result);

        return RequestResult<CandidateResponse>.Success(MapToResponse(result.Data!));
    }

    // Mesmo mapeamento do GetCandidateByIdUseCase — PUT /candidates/me não pode
    // zerar os contadores/campos que o GET retorna
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
        IsPhoneVerified = c.IsPhoneVerified,
        City = c.Address?.City,
        State = c.Address?.State,
        ResumesCount = c.Resumes?.Count ?? 0,
        ApplicationsCount = c.Applications?.Count ?? 0,
        CreatedAt = c.CreatedAt,
        UpdatedAt = c.UpdatedAt
    };

    // Nome e e-mail fazem parte do perfil salvo e são sempre validados; os demais
    // campos só entram na validação quando o pedido os traz.
    private static bool IsFieldInformedOnUpdate(string field, UpdateCandidateRequest request) => field switch
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
