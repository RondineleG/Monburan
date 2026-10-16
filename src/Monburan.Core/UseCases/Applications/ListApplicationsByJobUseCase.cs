using Monburan.Core.DTOs.Request;
using Monburan.Core.DTOs.Response;
using Monburan.Core.Entities;
using Monburan.Core.Enums;
using Monburan.Core.Interfaces.Repositories.Base;
using Monburan.Core.Interfaces.UseCases;
using Monburan.Core.Requests;
using Monburan.Core.ValueObjects;

namespace Monburan.Core.UseCases.Applications;

public class ListApplicationsByJobUseCase : IUseCase<ListApplicationsByJobRequest, IEnumerable<ApplicationResponse>>
{
    private readonly IBaseRepository<Application> _applicationRepository;

    public ListApplicationsByJobUseCase(IBaseRepository<Application> applicationRepository)
    {
        _applicationRepository = applicationRepository;
    }

    public async Task<RequestResult<IEnumerable<ApplicationResponse>>> ExecuteAsync(ListApplicationsByJobRequest request)
    {
        var result = await _applicationRepository.GetAllAsync(1, 1000);
        if (result.Status != EResultStatus.Success)
            return RequestResult<IEnumerable<ApplicationResponse>>.FromResult(result);

        var applications = result.Data!
            .Where(a => a.Job != null && a.Job.AdvertiserId.Value == request.AdvertiserId)
            .Where(a => !request.JobId.HasValue || a.JobId.Value == request.JobId.Value)
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
