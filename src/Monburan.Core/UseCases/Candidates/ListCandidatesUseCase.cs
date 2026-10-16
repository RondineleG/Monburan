using Monburan.Core.DTOs.Request;
using Monburan.Core.DTOs.Response;
using Monburan.Core.Entities;
using Monburan.Core.Enums;
using Monburan.Core.Interfaces.Repositories.Base;
using Monburan.Core.Interfaces.UseCases;
using Monburan.Core.Requests;

namespace Monburan.Core.UseCases.Candidates;

public sealed class ListCandidatesUseCase : IUseCase<ListCandidatesRequest, ListCandidatesResponse>
{
    private readonly IBaseRepository<Candidate> _candidateRepository;

    public ListCandidatesUseCase(IBaseRepository<Candidate> candidateRepository)
    {
        _candidateRepository = candidateRepository;
    }

    public async Task<RequestResult<ListCandidatesResponse>> ExecuteAsync(ListCandidatesRequest request)
    {
        var result = await _candidateRepository.GetAllAsync(request.Page, request.Size);
        if (result.Status != EResultStatus.Success)
            return RequestResult<ListCandidatesResponse>.FromResult(result);

        var candidates = result.Data!
            .Where(c => ShouldInclude(c, request))
            .Select(MapToResponse)
            .ToList();

        var response = new ListCandidatesResponse
        {
            Items = candidates,
            TotalCount = candidates.Count,
            Page = request.Page,
            Size = request.Size
        };

        return RequestResult<ListCandidatesResponse>.Success(response);
    }

    private static bool ShouldInclude(Candidate candidate, ListCandidatesRequest request)
    {
        if (!string.IsNullOrWhiteSpace(request.SearchTerm))
        {
            var term = request.SearchTerm.ToLower();
            if (!candidate.Name.ToLower().Contains(term) &&
                !candidate.Email.ToLower().Contains(term) &&
                !(candidate.Phone?.ToLower().Contains(term) ?? false))
            {
                return false;
            }
        }

        if (!string.IsNullOrWhiteSpace(request.Status) &&
            Enum.TryParse<EAccountStatus>(request.Status, true, out var statusFilter) &&
            candidate.Status != statusFilter)
        {
            return false;
        }

        return true;
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
