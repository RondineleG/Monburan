using Monburan.Core.DTOs.Request;
using Monburan.Core.DTOs.Response;
using Monburan.Core.Entities;
using Monburan.Core.Enums;
using Monburan.Core.Interfaces.Repositories;
using Monburan.Core.Interfaces.UseCases;
using Monburan.Core.Requests;

namespace Monburan.Core.UseCases.Candidates;

public sealed class ListCandidatesUseCase : IUseCase<ListCandidatesRequest, ListCandidatesResponse>
{
    private readonly ICandidateRepository _candidateRepository;

    public ListCandidatesUseCase(ICandidateRepository candidateRepository)
    {
        _candidateRepository = candidateRepository;
    }

    public async Task<RequestResult<ListCandidatesResponse>> ExecuteAsync(ListCandidatesRequest request)
    {
        var page = request.Page < 1 ? 1 : request.Page;
        var size = request.Size is < 1 or > 100 ? 20 : request.Size;
        EAccountStatus? status = Enum.TryParse<EAccountStatus>(request.Status, true, out var parsed) ? parsed : null;

        var result = await _candidateRepository.SearchAsync(request.SearchTerm, status, page, size);
        if (result.Status != EResultStatus.Success || result.Data is null)
            return RequestResult<ListCandidatesResponse>.FromResult(result);

        var response = new ListCandidatesResponse
        {
            Items = result.Data.Data.Select(MapToResponse).ToList(),
            TotalCount = result.Data.TotalCount,
            Page = page,
            Size = size
        };

        return RequestResult<ListCandidatesResponse>.Success(response);
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
