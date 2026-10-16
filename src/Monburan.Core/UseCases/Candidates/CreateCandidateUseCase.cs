using Monburan.Core.DTOs.Request;
using Monburan.Core.DTOs.Response;
using Monburan.Core.Entities;
using Monburan.Core.Enums;
using Monburan.Core.Interfaces.Repositories.Base;
using Monburan.Core.Interfaces.UseCases;
using Monburan.Core.Requests;
using Monburan.Core.ValueObjects;

namespace Monburan.Core.UseCases.Candidates;

public class CreateCandidateUseCase : IUseCase<CreateCandidateRequest, CandidateResponse>
{
    private readonly IBaseRepository<Candidate> _candidateRepository;

    public CreateCandidateUseCase(IBaseRepository<Candidate> candidateRepository)
    {
        _candidateRepository = candidateRepository;
    }

    public async Task<RequestResult<CandidateResponse>> ExecuteAsync(CreateCandidateRequest request)
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

        var result = await _candidateRepository.CreateAsync(candidate);
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
}
