using Monburan.Core.DTOs.Request;
using Monburan.Core.DTOs.Response;
using Monburan.Core.Entities;
using Monburan.Core.Enums;
using Monburan.Core.Interfaces.Repositories.Base;
using Monburan.Core.Interfaces.UseCases;
using Monburan.Core.Requests;
using Monburan.Core.ValueObjects;

namespace Monburan.Core.UseCases.Candidates;

public sealed class UpdateCandidateUseCase : IUseCase<UpdateCandidateRequest, CandidateResponse>
{
    private readonly IBaseRepository<Candidate> _candidateRepository;

    public UpdateCandidateUseCase(IBaseRepository<Candidate> candidateRepository)
    {
        _candidateRepository = candidateRepository;
    }

    public async Task<RequestResult<CandidateResponse>> ExecuteAsync(UpdateCandidateRequest request)
    {
        var getResult = await _candidateRepository.GetByIdAsync(Id.Create(request.Id));

        if (getResult.Status != EResultStatus.Success || getResult.Data == null)
            return RequestResult<CandidateResponse>.EntityNotFound("Candidate", request.Id, "Candidato não encontrado.");

        var candidate = getResult.Data;

        candidate.Name = request.Name;
        candidate.Phone = request.Phone;
        candidate.DateOfBirth = request.DateOfBirth;
        candidate.LinkedIn = request.LinkedIn;

        if (request.AddressId.HasValue)
        {
            candidate.AddressId = Id.Create(request.AddressId.Value);
        }

        var result = await _candidateRepository.UpdateAsync(candidate);

        if (result.Status != EResultStatus.Success)
            return RequestResult<CandidateResponse>.WithError(result.Message);

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
}
