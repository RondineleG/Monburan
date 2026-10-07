using Monburan.Core.DTOs.Request;
using Monburan.Core.DTOs.Response;
using Monburan.Core.Entities;
using Monburan.Core.Enums;
using Monburan.Core.Interfaces.Repositories;
using Monburan.Core.Interfaces.UseCases;
using Monburan.Core.Requests;

namespace Monburan.Core.UseCases.Jobs;

public class SearchJobsUseCase : IUseCase<ListJobsRequest, ListJobsResponse>
{
    private readonly IJobRepository _jobRepository;

    public SearchJobsUseCase(IJobRepository jobRepository)
    {
        _jobRepository = jobRepository;
    }

    public async Task<RequestResult<ListJobsResponse>> ExecuteAsync(ListJobsRequest request)
    {
        var result = await _jobRepository.SearchAsync(
            searchTerm: request.SearchTerm,
            city: request.City,
            state: request.State,
            isRemote: request.IsRemote,
            salaryMin: request.SalaryMin,
            salaryMax: request.SalaryMax,
            category: request.Category,
            advertiserId: request.AdvertiserId,
            page: request.Page,
            size: request.Size,
            type: request.Type);

        if (result.Status != EResultStatus.Success)
            return RequestResult<ListJobsResponse>.WithError(result.Message);

        var jobs = result.Data!
            .Select(MapToResponse)
            .ToList();

        var response = new ListJobsResponse
        {
            Jobs = jobs,
            TotalCount = jobs.Count,
            Page = request.Page,
            Size = request.Size
        };

        return RequestResult<ListJobsResponse>.Success(response);
    }

    private static JobResponse MapToResponse(Job job)
    {
        return new JobResponse
        {
            Id = job.Id.Value,
            Title = job.Title,
            Description = job.Description,
            Requirements = job.Requirements,
            Responsibilities = job.Responsibilities,
            Benefits = job.Benefits,
            ApplicationInstructions = job.ApplicationInstructions,
            NumberOfOpenings = job.NumberOfOpenings,
            Observations = job.Observations,
            Category = job.Category,
            Tags = job.Tags,
            ExpirationDate = job.ExpirationDate,
            Status = job.Status.ToString(),
            City = job.Location?.City,
            State = job.Location?.State,
            Country = job.Location?.Country ?? "BR",
            Type = job.Location?.Type ?? "OnSite",
            IsRemote = job.Location?.IsRemote ?? false,
            SalaryMin = job.Salary?.Min,
            SalaryMax = job.Salary?.Max,
            SalaryCurrency = job.Salary?.Currency ?? "BRL",
            SalaryDisplay = job.Salary?.Display ?? true,
            Views = job.Counters?.Views ?? 0,
            Applications = job.Counters?.Applications ?? 0,
            AdvertiserId = job.AdvertiserId.Value,
            AdvertiserName = job.Advertiser?.Name,
            AdvertiserLogoUrl = job.Advertiser?.LogoUrl,
            CreatedAt = job.CreatedAt,
            UpdatedAt = job.UpdatedAt
        };
    }
}
