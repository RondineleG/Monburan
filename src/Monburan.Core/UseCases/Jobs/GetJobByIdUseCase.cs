using Monburan.Core.DTOs.Response;
using Monburan.Core.Entities;
using Monburan.Core.Enums;
using Monburan.Core.Interfaces.Repositories.Base;
using Monburan.Core.Interfaces.UseCases;
using Monburan.Core.Requests;
using Monburan.Core.Services;
using Monburan.Core.ValueObjects;

namespace Monburan.Core.UseCases.Jobs;

public class GetJobByIdUseCase : IUseCase<Guid, JobResponse>
{
    private readonly IBaseRepository<Job> _jobRepository;

    public GetJobByIdUseCase(IBaseRepository<Job> jobRepository)
    {
        _jobRepository = jobRepository;
    }

    public async Task<RequestResult<JobResponse>> ExecuteAsync(Guid request)
    {
        var result = await _jobRepository.GetByIdAsync(Id.Create(request));
        if (result.Status != EResultStatus.Success)
            return result.Status == EResultStatus.EntityNotFound
                ? RequestResult<JobResponse>.EntityNotFound("Job", request, result.Message)
                : RequestResult<JobResponse>.FromResult(result);

        var job = result.Data!;
        var response = MapToResponse(job);

        return RequestResult<JobResponse>.Success(response);
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
            ModerationReason = JobModeration.VisibleReason(job),
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
