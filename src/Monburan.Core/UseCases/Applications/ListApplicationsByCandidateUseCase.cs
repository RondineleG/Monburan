using Monburan.Core.DTOs.Request;
using Monburan.Core.DTOs.Response;
using Monburan.Core.Entities;
using Monburan.Core.Enums;
using Monburan.Core.Interfaces.Repositories;
using Monburan.Core.Interfaces.UseCases;
using Monburan.Core.Requests;

namespace Monburan.Core.UseCases.Applications;

public class ListApplicationsByCandidateUseCase : IUseCase<ListApplicationsByCandidateRequest, IEnumerable<ApplicationResponse>>
{
    private readonly IApplicationRepository _applicationRepository;

    public ListApplicationsByCandidateUseCase(IApplicationRepository applicationRepository)
    {
        _applicationRepository = applicationRepository;
    }

    public async Task<RequestResult<IEnumerable<ApplicationResponse>>> ExecuteAsync(ListApplicationsByCandidateRequest request, CancellationToken cancellationToken = default)
    {
        var result = await _applicationRepository.ListByCandidateAsync(request.CandidateId, request.Page, request.Size, cancellationToken: cancellationToken);
        if (result.Status != EResultStatus.Success)
            return RequestResult<IEnumerable<ApplicationResponse>>.FromResult(result);

        var applications = result.Data!
            .Select(MapToResponse)
            .OrderByDescending(a => a.CreatedAt);

        return RequestResult<IEnumerable<ApplicationResponse>>.Success(applications);
    }

    private static ApplicationResponse MapToResponse(Application app) => new()
    {
        Id = app.Id.Value,
        CandidateId = app.CandidateId.Value,
        CandidateName = app.Candidate?.Name,
        CandidateEmail = app.Candidate?.Email,
        JobId = app.JobId.Value,
        JobTitle = app.Job?.Title,
        AdvertiserName = app.Job?.Advertiser?.Name,
        Status = app.Status.ToString(),
        AppliedAt = app.AppliedAt,
        ResumeId = app.ResumeId,
        ViewedAt = app.ViewedAt,
        SelectedAt = app.SelectedAt,
        RejectedAt = app.RejectedAt,
        Notes = app.Notes,
        CreatedAt = app.CreatedAt
    };
}
