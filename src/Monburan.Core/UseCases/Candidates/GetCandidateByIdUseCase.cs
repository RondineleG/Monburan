using Monburan.Core.DTOs.Response;
using Monburan.Core.Entities;
using Monburan.Core.Enums;
using Monburan.Core.Interfaces.Repositories.Base;
using Monburan.Core.Interfaces.UseCases;
using Monburan.Core.Requests;
using Monburan.Core.ValueObjects;

namespace Monburan.Core.UseCases.Candidates;

public sealed class GetCandidateByIdUseCase : IUseCase<Guid, CandidateResponse>
{
    private readonly IBaseRepository<Candidate> _candidateRepository;

    public GetCandidateByIdUseCase(IBaseRepository<Candidate> candidateRepository)
    {
        _candidateRepository = candidateRepository;
    }

    public async Task<RequestResult<CandidateResponse>> ExecuteAsync(Guid request, CancellationToken cancellationToken = default)
    {
        var result = await _candidateRepository.GetByIdAsync(Id.Create(request), cancellationToken);
        if (result.Status != EResultStatus.Success)
            return RequestResult<CandidateResponse>.FromResult(result);

        var candidate = result.Data!;
        var response = MapToResponse(candidate);

        return RequestResult<CandidateResponse>.Success(response);
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
        IsPhoneVerified = c.IsPhoneVerified,
        City = c.Address?.City,
        State = c.Address?.State,
        ResumesCount = c.Resumes?.Count ?? 0,
        ApplicationsCount = c.Applications?.Count ?? 0,
        CreatedAt = c.CreatedAt,
        UpdatedAt = c.UpdatedAt
    };
}
