using Monburan.Core.DTOs.Request;
using Monburan.Core.DTOs.Response;
using Monburan.Core.Entities;
using Monburan.Core.Enums;
using Monburan.Core.Interfaces.Repositories.Base;
using Monburan.Core.Interfaces.UseCases;
using Monburan.Core.Interfaces.Validations;
using Monburan.Core.Requests;
using Monburan.Core.UseCases.Subscriptions;
using Monburan.Core.ValueObjects;

namespace Monburan.Core.UseCases.Jobs;

public class CreateJobUseCase : IUseCase<CreateJobRequest, JobResponse>
{
    private readonly IBaseRepository<Job> _jobRepository;
    private readonly IEntityValidator<Job> _jobValidator;
    private readonly CheckJobLimitUseCase _checkJobLimit;

    public CreateJobUseCase(
        IBaseRepository<Job> jobRepository,
        IEntityValidator<Job> jobValidator,
        CheckJobLimitUseCase checkJobLimit)
    {
        _jobRepository = jobRepository;
        _jobValidator = jobValidator;
        _checkJobLimit = checkJobLimit;
    }

    public async Task<RequestResult<JobResponse>> ExecuteAsync(CreateJobRequest request)
    {
        var job = new Job
        {
            Id = Id.Create(Guid.NewGuid()),
            AdvertiserId = Id.Create(request.AdvertiserId),
            Title = request.Title,
            Description = request.Description,
            Requirements = request.Requirements,
            Responsibilities = request.Responsibilities,
            Benefits = request.Benefits,
            ApplicationInstructions = request.ApplicationInstructions,
            NumberOfOpenings = request.NumberOfOpenings,
            Observations = request.Observations,
            Category = request.Category,
            Tags = request.Tags,
            ExpirationDate = request.ExpirationDate,
            Status = EJobStatus.Active,
            TenantId = "default",
            Location = new JobLocation
            {
                City = request.City,
                State = request.State,
                Country = request.Country,
                Type = request.Type,
                IsRemote = request.IsRemote,
                ShowAddress = request.ShowAddress
            },
            Salary = new SalaryRange
            {
                Min = request.SalaryMin,
                Max = request.SalaryMax,
                Currency = request.SalaryCurrency,
                Display = request.SalaryDisplay
            },
            Moderation = new ModerationInfo { Status = EModerationStatus.Pending },
            Boost = new JobBoost { IsActive = false },
            Highlight = new JobHighlight { IsActive = false },
            Counters = new JobCounters()
        };

        var validationResult = _jobValidator.Validate(job);
        if (!validationResult.IsValid)
        {
            var createResult = RequestResult<JobResponse>.WithNoContent();
            foreach (var error in validationResult.Errors)
            {
                createResult.ValidationResult.AddError(error.Message, error.Field);
            }
            return createResult;
        }

        // Limite de vagas ativas do plano — a página de planos promete esse limite
        var limitResult = await _checkJobLimit.ExecuteAsync(new CheckJobLimitRequest(request.AdvertiserId));
        if (limitResult.Status != EResultStatus.Success)
            return RequestResult<JobResponse>.FromResult(limitResult);

        if (!limitResult.Data!.CanCreate)
        {
            return RequestResult<JobResponse>.WithDomainError(
                $"Seu plano {limitResult.Data.PlanName} permite até {limitResult.Data.MaxJobs} vagas ativas. " +
                "Encerre uma vaga ou mude de plano para publicar outra.");
        }

        var createJobResult = await _jobRepository.CreateAsync(job);
        if (createJobResult.Status != EResultStatus.Success)
            return RequestResult<JobResponse>.FromResult(createJobResult);

        return RequestResult<JobResponse>.Success(MapToResponse(createJobResult.Data!));
    }

    private static JobResponse MapToResponse(Job job) => new()
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
        CreatedAt = job.CreatedAt,
        UpdatedAt = job.UpdatedAt
    };
}
